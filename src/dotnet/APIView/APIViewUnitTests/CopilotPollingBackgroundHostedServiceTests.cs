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
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
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
        private readonly Mock<IPollingJobQueueManager> _mockPollingJobQueueManager;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
        private readonly Mock<ILogger<CopilotPollingBackgroundHostedService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly CopilotPollingBackgroundHostedService _service;

        public CopilotPollingBackgroundHostedServiceTests()
        {
            _mockPollingJobQueueManager = new Mock<IPollingJobQueueManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            _mockLogger = new Mock<ILogger<CopilotPollingBackgroundHostedService>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            Mock<IConfiguration> mockConfiguration = new();
            mockConfiguration.Setup(x => x["CopilotServiceEndpoint"])
                .Returns("https://test-copilot-endpoint.com");

            Mock<IHttpClientFactory> mockHttpClientFactory = new();
            HttpClient httpClient = new(this._mockHttpMessageHandler.Object);
            mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            _service = new CopilotPollingBackgroundHostedService(
                _mockPollingJobQueueManager.Object,
                mockConfiguration.Object,
                mockHttpClientFactory.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsRepository.Object,
                _mockSignalRHubContext.Object,
                _mockLogger.Object);
        }

        [Theory]
        [InlineData("Success", true, false)]
        [InlineData("Error", false, false)]
        [InlineData("Success", false, true)]
        public async Task ExecuteAsync_DifferentScenarios_HandlesCorrectly(
            string responseStatus, bool shouldProcessComments, bool throwHttpException)
        {
            AIReviewJobInfoModel jobInfo = CreateTestJobInfo();
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = responseStatus,
                Details = responseStatus == "Success" ? "Review completed successfully" : "Analysis failed",
                Comments = CreateSuccessfulPollResponse().Comments
            };

            await ExecuteTestScenario(jobInfo, pollResponse, throwHttpException);

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

            if (throwHttpException)
            {
                VerifyErrorLogged();
            }
        }

        [Theory]
        [InlineData("any")]
        [InlineData("summary")]
        public async Task ExecuteAsync_CommentTypes_FormatsCorrectly(string commentType)
        {
            var comment = commentType == "any"
                ? CreateCommentWithSuggestions()
                : CreateSummaryComment();

            CommentItemModel capturedComment = await ProcessCommentAndGetText(comment);

            switch (commentType)
            {
                case "any":
                    Assert.NotEqual("FIRST_ROW", capturedComment.ElementId ?? "");
                    Assert.Contains("Test suggestion", capturedComment.CommentText);
                    Assert.Contains("https://azure.github.io/azure-sdk/", capturedComment.CommentText);
                    Assert.Equal(ApiViewConstants.AzureSdkBotName, capturedComment.CreatedBy);
                    Assert.False(capturedComment.ResolutionLocked);
                    break;
                case "summary":
                    Assert.Equal("FIRST_ROW", capturedComment.ElementId);
                    break;
            }
        }

        [Theory]
        [InlineData(true, new[] { "dotnet-client-design", "dotnet-naming-conventions" })]
        [InlineData(false, new string[0])]
        public async Task ExecuteAsync_GuidelinesHandling_FormatsCorrectly(bool shouldIncludeGuidelines,
            string[] ruleIds)
        {
            AIReviewComment comment = CreateAIReviewComment(ruleIds.ToList());
            CommentItemModel capturedComment = await ProcessCommentAndGetText(comment);
            string commentText = capturedComment.CommentText;

            if (shouldIncludeGuidelines)
            {
                Assert.Contains("**Guidelines**", commentText);
                foreach (var ruleId in ruleIds)
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

        private AIReviewJobInfoModel CreateTestJobInfo()
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
                CodeLines =
                [
                    ("public class TestClass", "line-1"),
                    ("public void TestMethod()", "line-2"),
                    ("// Summary comment", null)
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
                        RuleIds = ["test-rule-1"],
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

        private async Task ExecuteTestScenario(AIReviewJobInfoModel jobInfo,
            AIReviewJobPolledResponseModel pollResponse, bool throwHttpException = false)
        {
            SetupJobProcessing(jobInfo, pollResponse);

            if (throwHttpException)
            {
                _mockHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(new HttpRequestException("Network error"));
            }

            SetupSignalRMock();

            var revisionUpdateCompleted = new TaskCompletionSource<bool>();
            _mockApiRevisionsManager.Setup(x => x.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
                .Callback<APIRevisionListItemModel>(_ => revisionUpdateCompleted.SetResult(true))
                .Returns(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            await _service.StartAsync(cancellationTokenSource.Token);
            
            await revisionUpdateCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        private void SetupSignalRMock()
        {
            var mockClientProxy = new Mock<IClientProxy>();
            _mockSignalRHubContext.Setup(x => x.Clients.All).Returns(mockClientProxy.Object);
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
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error while polling Copilot job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.AtLeastOnce);
        }

        private void SetupJobProcessing(AIReviewJobInfoModel jobInfo, AIReviewJobPolledResponseModel pollResponse)
        {
            _mockPollingJobQueueManager.Setup(x => x.TryDequeue(out jobInfo))
                .Returns(true)
                .Callback(() =>
                {
                    AIReviewJobInfoModel nullJob = null;
                    _mockPollingJobQueueManager.Setup(x => x.TryDequeue(out nullJob))
                        .Returns(false);
                });

            SetupHttpClientForSuccessfulPolling(pollResponse);
        }

        private AIReviewComment CreateAIReviewComment(List<string> ruleIds)
        {
            return new AIReviewComment
            {
                LineNo = 1,
                Comment = "Test comment",
                Source = "analysis",
                RuleIds = ruleIds,
                Suggestion = "Test suggestion",
                Code = "public void Method()"
            };
        }

        private async Task<CommentItemModel> ProcessCommentAndGetText(AIReviewComment comment)
        {
            var jobInfo = CreateTestJobInfo();
            var pollResponse = new AIReviewJobPolledResponseModel
            {
                Status = "Success",
                Details = "Review completed successfully",
                Comments = [comment]
            };

            SetupJobProcessing(jobInfo, pollResponse);
            SetupSignalRMock();

            CommentItemModel capturedComment = null;
            var commentCaptured = new TaskCompletionSource<bool>();
            
            _mockCommentsRepository.Setup(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()))
                .Callback<CommentItemModel>(c => 
                {
                    capturedComment = c;
                    commentCaptured.SetResult(true); // Signal that comment was captured
                })
                .Returns(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(2000);

            await _service.StartAsync(cancellationTokenSource.Token);
            
            await commentCaptured.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.NotNull(capturedComment);
            return capturedComment;
        }

        private AIReviewComment CreateCommentWithSuggestions()
        {
            return new AIReviewComment
            {
                LineNo = 1,
                Comment = "Consider improving this method",
                Source = "analysis",
                RuleIds = new List<string> { "design-rule-1", "naming-rule-2" },
                Suggestion = "Test suggestion",
                Code = "public class TestClass"
            };
        }

        private AIReviewComment CreateSummaryComment()
        {
            return new AIReviewComment
            {
                LineNo = 3,
                Comment = "Overall summary of the review",
                Source = "summary",
                RuleIds = new List<string>(),
                Suggestion = null,
                Code = "// Summary comment"
            };
        }
    }
}
