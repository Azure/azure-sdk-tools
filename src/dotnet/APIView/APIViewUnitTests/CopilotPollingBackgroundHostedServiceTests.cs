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
    public class CopilotPollingBackgroundHostedServiceTests : IAsyncLifetime
    {
        private readonly Mock<IPollingJobQueueManager> _mockPollingJobQueueManager;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
        private readonly Mock<ILogger<CopilotPollingBackgroundHostedService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly HttpClient _httpClient;
        private CopilotPollingBackgroundHostedService _service;

        public CopilotPollingBackgroundHostedServiceTests()
        {
            _mockPollingJobQueueManager = new Mock<IPollingJobQueueManager>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            _mockLogger = new Mock<ILogger<CopilotPollingBackgroundHostedService>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["CopilotServiceEndpoint"])
                .Returns("https://test-copilot-endpoint.com");

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(_httpClient);
        }

        private CopilotPollingBackgroundHostedService CreateService()
        {
            return new CopilotPollingBackgroundHostedService(
                _mockPollingJobQueueManager.Object,
                _mockConfiguration.Object,
                _mockHttpClientFactory.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsRepository.Object,
                _mockSignalRHubContext.Object,
                _mockLogger.Object);
        }

        public async Task InitializeAsync()
        {
            ResetAllMocks();
            _service = CreateService();
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_service != null)
            {
                try
                {
                    await _service.StopAsync(CancellationToken.None);
                }
                catch
                {
                    // Swallow exceptions during disposal to prevent masking test failures
                }
                _service.Dispose();
                _service = null;
            }
            await Task.CompletedTask;
        }

        private void ResetAllMocks()
        {
            _mockPollingJobQueueManager.Reset();
            _mockApiRevisionsManager.Reset();
            _mockCommentsRepository.Reset();
            _mockSignalRHubContext.Reset();
            _mockLogger.Reset();
            _mockHttpMessageHandler.Reset();
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

        [Theory(Skip = "https://github.com/Azure/azure-sdk-tools/issues/11249")]
        [InlineData("summary", 1, "FIRST_ROW")]
        [InlineData("summary", 2, "line-2")]
        [InlineData("any", 2, "line-2")]
        public async Task ExecuteAsync_CommentTypes_FormatsCorrectly(string commentType, int lineNo, string expectedElementId)
        {
            List<(string lineText, string lineId)> codeLinesList = new()
            {
                (string.Empty, null),
                ("public class TestClass", "line-2"),
                ("public void TestMethod()", "line-3"),
            };

            var comment = commentType == "summary"
                ? CreateSummaryComment(lineNo)
                : CreateCommentWithSuggestions(lineNo);

            CommentItemModel capturedComment = await ProcessCommentAndGetText(comment, CreateTestJobInfo(codeLinesList));
            Assert.Equal(expectedElementId, capturedComment.ElementId);
        }

        [Fact(Skip = "https://github.com/Azure/azure-sdk-tools/issues/11249")]
        public async Task ExecuteAsync_CommentTypes_NotSummary_NotElementId_Skipped()
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

            SetupJobProcessing(jobInfo, pollResponse);
            SetupSignalRMock();

            var revisionUpdateCompleted = new TaskCompletionSource<bool>();
            _mockApiRevisionsManager.Setup(x => x.UpdateAPIRevisionAsync(It.IsAny<APIRevisionListItemModel>()))
                .Callback<APIRevisionListItemModel>(_ => revisionUpdateCompleted.SetResult(true))
                .Returns(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            await _service.StartAsync(cancellationTokenSource.Token);
            
            await revisionUpdateCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            _mockCommentsRepository.Verify(x => x.UpsertCommentAsync(It.IsAny<CommentItemModel>()), Times.Never);
        }

        [Theory(Skip = "https://github.com/Azure/azure-sdk-tools/issues/11249")]
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
                .Callback<APIRevisionListItemModel>(_ => 
                {
                    if (!revisionUpdateCompleted.Task.IsCompleted)
                        revisionUpdateCompleted.SetResult(true);
                })
                .Returns(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
            await _service.StartAsync(cancellationTokenSource.Token);
            
            await revisionUpdateCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            
            cancellationTokenSource.Cancel();
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

        private async Task<CommentItemModel> ProcessCommentAndGetText(AIReviewComment comment, AIReviewJobInfoModel reviewJobInfo = null)
        {
            var jobInfo = reviewJobInfo ?? CreateTestJobInfo();
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
                    if (!commentCaptured.Task.IsCompleted)
                        commentCaptured.SetResult(true);
                })
                .Returns(Task.CompletedTask);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));

            await _service.StartAsync(cancellationTokenSource.Token);
            
            await commentCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

            cancellationTokenSource.Cancel();

            Assert.NotNull(capturedComment);
            return capturedComment;
        }

        private AIReviewComment CreateCommentWithSuggestions(int lineNo = 1)
        {
            return new AIReviewComment
            {
                LineNo = lineNo,
                Comment = "Consider improving this method",
                Source = "analysis",
                RuleIds = new List<string> { "design-rule-1", "naming-rule-2" },
                Suggestion = "Test suggestion",
                Code = "public class TestClass"
            };
        }

        private AIReviewComment CreateSummaryComment(int lineNo =3)
        {
            return new AIReviewComment
            {
                LineNo = lineNo,
                Comment = "Overall summary of the review",
                Source = "summary",
                RuleIds = new List<string>(),
                Suggestion = null,
                Code = "// Summary comment"
            };
        }
    }
}
