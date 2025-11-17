// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.HostedServices;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace APIViewUnitTests
{
    public class CopilotPollingBackgroundHostedServiceTests
    {
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
        private readonly Mock<ICopilotAuthenticationService> _mockCopilotAuth;
        private readonly Mock<ILogger<CopilotJobProcessor>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly HttpClient _httpClient;
        private readonly CopilotJobProcessor _processor;

        public CopilotPollingBackgroundHostedServiceTests()
        {
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            _mockCopilotAuth = new Mock<ICopilotAuthenticationService>();   
            _mockLogger = new Mock<ILogger<CopilotJobProcessor>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["CopilotServiceEndpoint"])
                .Returns("https://test-copilot-endpoint.com");

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);

            // Setup SignalR mock
            var mockClientProxy = new Mock<IClientProxy>();
            _mockSignalRHubContext.Setup(x => x.Clients.All).Returns(mockClientProxy.Object);

            _processor = new CopilotJobProcessor(
                _mockConfiguration.Object,
                _mockHttpClientFactory.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsRepository.Object,
                _mockCopilotAuth.Object,
                _mockSignalRHubContext.Object,
                _mockLogger.Object);
        }

        [Theory]
        [InlineData("Success", true)]
        [InlineData("Error", false)]
        public async Task ProcessJobAsync_DifferentScenarios_HandlesCorrectly(
            string responseStatus, bool shouldProcessComments)
        {
            AIReviewJobInfoModel jobInfo = CreateTestJobInfo();
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = responseStatus,
                Details = responseStatus == "Success" ? "Review completed successfully" : "Analysis failed",
                Comments = CreateSuccessfulPollResponse().Comments
            };

            SetupHttpClientForSuccessfulPolling(pollResponse);

            if (responseStatus == "Error")
            {
                var exception = await Assert.ThrowsAsync<Exception>(() => _processor.ProcessJobAsync(jobInfo));
                Assert.Equal("Analysis failed", exception.Message);
            }
            else
            {
                await _processor.ProcessJobAsync(jobInfo);
            }

            if (shouldProcessComments)
            {
                _mockCommentsRepository.Verify(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()),
                    Times.AtLeastOnce);
                VerifySignalRCalled();
            }
            else
            {
                _mockCommentsRepository.Verify(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
            }

            _mockApiRevisionsManager.Verify(x => x.UpdateAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(api => !api.CopilotReviewInProgress)), Times.AtLeastOnce);
        }

        [Theory]
        [InlineData("summary", 1, "FIRST_ROW")]
        [InlineData("summary", 2, "line-2")]
        [InlineData("any", 2, "line-2")]
        public async Task ProcessJobAsync_CommentTypes_FormatsCorrectly(string commentType, int lineNo, string expectedElementId)
        {
            // Arrange
            List<(string lineText, string lineId)> codeLinesList = new()
            {
                (string.Empty, null),
                ("public class TestClass", "line-2"),
                ("public void TestMethod()", "line-3"),
            };

            var comment = commentType == "summary"
                ? CreateSummaryComment(lineNo)
                : CreateCommentWithSuggestions(lineNo);

            var jobInfo = CreateTestJobInfo(codeLinesList);
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = "Success",
                Details = "Review completed successfully",
                Comments = [comment]
            };

            SetupHttpClientForSuccessfulPolling(pollResponse);

            CommentItemModel capturedComment = null;
            _mockCommentsRepository.Setup(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
                .Callback<CommentItemModel>(c => capturedComment = c)
                .Returns(Task.CompletedTask);

            await _processor.ProcessJobAsync(jobInfo);

            Assert.NotNull(capturedComment);
            Assert.Equal(expectedElementId, capturedComment.ElementId);
        }

        [Fact]
        public async Task ProcessJobAsync_CommentTypes_NotSummary_NotElementId_Skipped()
        {
            List<(string lineText, string lineId)> codeLinesList =
            [
                (string.Empty, null),
                ("public class TestClass", "line-2"),
                ("public void TestMethod()", "line-3"),
            ];

            var comment = CreateCommentWithSuggestions(1);
            var jobInfo = CreateTestJobInfo(codeLinesList);
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = "Success",
                Details = "Review completed successfully",
                Comments = [comment]
            };

            SetupHttpClientForSuccessfulPolling(pollResponse);

            await _processor.ProcessJobAsync(jobInfo);

            _mockCommentsRepository.Verify(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
        }

        [Theory]
        [InlineData(true, new[] { "dotnet-client-design", "dotnet-naming-conventions" })]
        [InlineData(false, new string[0])]
        public async Task ProcessJobAsync_GuidelinesHandling_FormatsCorrectly(bool shouldIncludeGuidelines,
            string[] guidelineIds)
        {
            AIReviewComment comment = CreateAIReviewComment(guidelineIds.ToList());
            var jobInfo = CreateTestJobInfo();
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = "Success",
                Details = "Review completed successfully",
                Comments = [comment]
            };

            SetupHttpClientForSuccessfulPolling(pollResponse);

            CommentItemModel capturedComment = null;
            _mockCommentsRepository.Setup(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
                .Callback<CommentItemModel>(c => capturedComment = c)
                .Returns(Task.CompletedTask);

            await _processor.ProcessJobAsync(jobInfo);

            Assert.NotNull(capturedComment);
            string commentText = capturedComment.CommentText;

            if (shouldIncludeGuidelines)
            {
                Assert.Contains("**Guidelines**", commentText);
                foreach (var ruleId in guidelineIds)
                {
                    Assert.Contains($"https://azure.github.io/azure-sdk/{ruleId}", commentText);
                }
            }
            else
            {
                Assert.DoesNotContain("**Guidelines**", commentText);
                Assert.DoesNotContain("https://azure.github.io/azure-sdk/", commentText);
            }

            Assert.Contains("Test comment", commentText);
            Assert.Contains("Suggestion : `Test suggestion`", commentText);
        }

        [Fact]
        public async Task ProcessJobAsync_HttpException_HandlesCorrectly()
        {
            AIReviewJobInfoModel jobInfo = CreateTestJobInfo();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _processor.ProcessJobAsync(jobInfo));
            Assert.Equal("Network error", exception.Message);

            _mockApiRevisionsManager.Verify(x => x.UpdateAPIRevisionAsync(
                It.Is<APIRevisionListItemModel>(api => !api.CopilotReviewInProgress)), Times.AtLeastOnce);

            VerifyErrorLogged();
        }

        private AIReviewJobInfoModel CreateTestJobInfo(List<(string lineText, string lineId)> codeLinesList = null)
        {
            return new AIReviewJobInfoModel
            {
                JobId = "test-job-123",
                CreatedBy = "testuser",
                APIRevision =
                    new APIRevisionListItemModel
                    {
                        Id = "revision-123", ReviewId = "review-456", CopilotReviewInProgress = true
                    },
                CodeLines = codeLinesList ??
                [
                    ("public class TestClass", "line-1"),
                    ("public void TestMethod()", "line-2"),
                ]
            };
        }

        private AIReviewJobPolledResponseModel CreateSuccessfulPollResponse()
        {
            return new AIReviewJobPolledResponseModel
            {
                Status = "Success",
                Details = "Review completed successfully",
                Comments =
                [
                    new AIReviewComment
                    {
                        LineNo = 1,
                        Comment = "This is a test comment",
                        Source = "analysis",
                        GuidelineIds = ["test-rule-1"],
                        Suggestion = null,
                        Code = "public class TestClass"
                    }
                ]
            };
        }

        private void SetupHttpClientForSuccessfulPolling(AIReviewJobPolledResponseModel pollResponse)
        {
            string jsonResponse = JsonSerializer.Serialize(pollResponse);
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponseMessage);
        }

        private void VerifySignalRCalled()
        {
            _mockSignalRHubContext.Verify(x => x.Clients.All.SendCoreAsync("ReceiveAIReviewUpdates",
                It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        private void VerifyErrorLogged()
        {
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing Copilot job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }

        private AIReviewComment CreateAIReviewComment(List<string> guidelineIds)
        {
            return new AIReviewComment
            {
                LineNo = 1,
                Comment = "Test comment",
                Source = "analysis",
                GuidelineIds = guidelineIds,
                Suggestion = "Test suggestion",
                Code = "public void Method()"
            };
        }

        private AIReviewComment CreateCommentWithSuggestions(int lineNo = 1)
        {
            return new AIReviewComment
            {
                LineNo = lineNo,
                Comment = "Consider improving this method",
                Source = "analysis",
                GuidelineIds = new List<string> { "design-rule-1", "naming-rule-2" },
                Suggestion = "Test suggestion",
                Code = "public class TestClass"
            };
        }

        private AIReviewComment CreateSummaryComment(int lineNo = 3)
        {
            return new AIReviewComment
            {
                LineNo = lineNo,
                Comment = "Overall summary of the review",
                Source = "summary",
                GuidelineIds = new List<string>(),
                Suggestion = null,
                Code = "// Summary comment"
            };
        }
    }
}
