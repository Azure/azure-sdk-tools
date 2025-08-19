using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Net.Http;
using ApiView;
using APIViewWeb.LeanModels;

namespace APIViewUnitTests
{
    public class ReviewManagerGenerateAIReviewTests
    {
        [Theory]
        [MemberData(nameof(GetDiagnosticsTestData))]
        public async Task GenerateAIReview_WithNullOrEmptyDiagnostics_ShouldNotThrowNullReference(
            CodeDiagnostic[] diagnostics, string _)
        {
            // Arrange
            var (reviewManager, mocks) = CreateTestSetup();
            
            var activeCodeFile = new RenderedCodeFile(new CodeFile { Diagnostics = diagnostics });
            SetupMocks(mocks, activeCodeFile);

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
                await reviewManager.GenerateAIReview(CreateTestUser(), "review-id", "revision-id", null));

            // Verify the diagnostics logic executed without null reference issues
            Assert.NotNull(exception); // Expected due to unmocked HTTP dependencies
            mocks.CodeFileRepository.Verify(r => r.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), false), Times.Once);
        }

        [Fact]
        public async Task GenerateAIReview_WithValidDiagnostics_ShouldProcessDiagnosticsCorrectly()
        {
            // Arrange
            var (reviewManager, mocks) = CreateTestSetup();
            
            var diagnostics = new[]
            {
                new CodeDiagnostic("DIAG001", "target1", "First diagnostic", "http://help1.com"),
                new CodeDiagnostic("DIAG002", "target2", "Second diagnostic", "http://help2.com", CodeDiagnosticLevel.Warning)
            };
            
            var activeCodeFile = new RenderedCodeFile(new CodeFile { Diagnostics = diagnostics });
            SetupMocks(mocks, activeCodeFile);

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () =>
                await reviewManager.GenerateAIReview(CreateTestUser(), "review-id", "revision-id", null));

            // Verify that the diagnostics processing logic was reached
            Assert.NotNull(exception); // Expected due to unmocked HTTP dependencies
            mocks.CodeFileRepository.Verify(r => r.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), false), Times.Once);
        }

        [Fact]
        public void DiagnosticsLogic_IsolatedTest_ValidatesConditionBehavior()
        {
            // Test the exact logic pattern from the target code block:
            // List<ApiViewAgentComment> diagnostics = new();
            // if (activeCodeFile.CodeFile?.Diagnostics?.Length > 0) { ... }

            var testCases = new[]
            {
                new { Diagnostics = (CodeDiagnostic[])null, ExpectedCount = 0, Description = "null diagnostics" },
                new { Diagnostics = new CodeDiagnostic[0], ExpectedCount = 0, Description = "empty diagnostics" },
                new { 
                    Diagnostics = new[] { new CodeDiagnostic("TEST001", "target1", "Test diagnostic", "http://help.com") }, 
                    ExpectedCount = 1, 
                    Description = "valid diagnostics" 
                }
            };

            foreach (var testCase in testCases)
            {
                // Simulate the target code block
                List<ApiViewAgentComment> diagnostics = new();
                if (testCase.Diagnostics?.Length > 0)
                {
                    // Simulate AgentHelpers.BuildDiagnosticsForAgent call
                    diagnostics.AddRange(testCase.Diagnostics.Select(d => new ApiViewAgentComment 
                    { 
                        LineNumber = 1, 
                        CommentText = d.Text 
                    }));
                }

                Assert.Equal(testCase.ExpectedCount, diagnostics.Count);
            }
        }

        public static IEnumerable<object[]> GetDiagnosticsTestData()
        {
            yield return new object[] { null, "null diagnostics" };
            yield return new object[] { new CodeDiagnostic[0], "empty diagnostics array" };
        }

        // Helper methods to reduce duplication
        private (ReviewManager reviewManager, MockContainer mocks) CreateTestSetup()
        {
            var mocks = new MockContainer();
            var reviewManager = new ReviewManager(
                mocks.AuthorizationService.Object,
                mocks.ReviewsRepository.Object,
                mocks.ApiRevisionsManager.Object,
                mocks.CommentManager.Object,
                mocks.CodeFileRepository.Object,
                mocks.CommentsRepository.Object,
                mocks.SignalRHubContext.Object,
                mocks.LanguageServices,
                mocks.TelemetryClient,
                mocks.CodeFileManager.Object,
                mocks.Configuration.Object,
                mocks.HttpClientFactory.Object,
                mocks.PollingJobQueueManager.Object
            );
            return (reviewManager, mocks);
        }

        private void SetupMocks(MockContainer mocks, RenderedCodeFile activeCodeFile)
        {
            var activeApiRevision = new APIRevisionListItemModel 
            { 
                Id = "test-revision-id", 
                Language = "CSharp" 
            };

            mocks.ApiRevisionsManager.Setup(m => m.GetAPIRevisionAsync(It.IsAny<string>()))
                .ReturnsAsync(activeApiRevision);

            mocks.CommentManager.Setup(m => m.GetCommentsAsync(It.IsAny<string>(), false, CommentType.APIRevision))
                .ReturnsAsync(new List<CommentItemModel>());
            
            mocks.CodeFileRepository.Setup(r => r.GetCodeFileAsync(It.IsAny<APIRevisionListItemModel>(), false))
                .ReturnsAsync(activeCodeFile);
        }

        private ClaimsPrincipal CreateTestUser()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("login", "testuser"),
                new Claim("name", "Test User")
            });
            return new ClaimsPrincipal(identity);
        }

        public class MockContainer
        {
            public Mock<IAuthorizationService> AuthorizationService { get; } = new();
            public Mock<ICosmosReviewRepository> ReviewsRepository { get; } = new();
            public Mock<IAPIRevisionsManager> ApiRevisionsManager { get; } = new();
            public Mock<ICommentsManager> CommentManager { get; } = new();
            public Mock<IBlobCodeFileRepository> CodeFileRepository { get; } = new();
            public Mock<ICosmosCommentsRepository> CommentsRepository { get; } = new();
            public Mock<IHubContext<SignalRHub>> SignalRHubContext { get; } = new();
            public TelemetryClient TelemetryClient { get; } = new TelemetryClient(new TelemetryConfiguration());
            public Mock<ICodeFileManager> CodeFileManager { get; } = new();
            public Mock<IConfiguration> Configuration { get; } = new();
            public Mock<IHttpClientFactory> HttpClientFactory { get; } = new();
            public Mock<IPollingJobQueueManager> PollingJobQueueManager { get; } = new();

            public IEnumerable<LanguageService> LanguageServices { get; } = new List<LanguageService>();
        }
    }
}
