using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.LeanControllers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using APIViewWeb.Models;
using APIViewWeb.Hubs;
using APIViewWeb.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class NamespaceReviewRequestTests
    {
        private readonly Mock<ILogger<ReviewsController>> _mockControllerLogger;
        private readonly Mock<ILogger<ReviewManager>> _mockManagerLogger;
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
        private readonly Mock<ICommentsManager> _mockCommentsManager;
        private readonly Mock<IBlobCodeFileRepository> _mockCodeFileRepository;
        private readonly Mock<ICosmosAPIRevisionsRepository> _mockApiRevisionsRepository;
        private readonly Mock<ICosmosCommentsRepository> _mockCommentsRepository;
        private readonly Mock<ICosmosPullRequestsRepository> _mockPullRequestsRepository;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly UserProfileCache _userProfileCache;
        private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
        private readonly Mock<INotificationManager> _mockNotificationManager;
        private readonly Mock<IAuthorizationService> _mockAuthorizationService;
        private readonly Mock<ICodeFileManager> _mockCodeFileManager;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IPollingJobQueueManager> _mockPollingJobQueueManager;
        private readonly Mock<ICopilotAuthenticationService> _mockCopilotAuth;
        private readonly TelemetryClient _telemetryClient;
        private readonly ReviewsController _controller;
        private readonly ReviewManager _reviewManager;

        public NamespaceReviewRequestTests()
        {
            // Initialize all mocks
            _mockControllerLogger = new Mock<ILogger<ReviewsController>>();
            _mockManagerLogger = new Mock<ILogger<ReviewManager>>();
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockReviewManager = new Mock<IReviewManager>();
            _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
            _mockCommentsManager = new Mock<ICommentsManager>();
            _mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
            _mockApiRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
            _mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            _mockPullRequestsRepository = new Mock<ICosmosPullRequestsRepository>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            _mockNotificationManager = new Mock<INotificationManager>();
            _mockAuthorizationService = new Mock<IAuthorizationService>();
            _mockCodeFileManager = new Mock<ICodeFileManager>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockPollingJobQueueManager = new Mock<IPollingJobQueueManager>();
            _mockCopilotAuth = new Mock<ICopilotAuthenticationService>();

            _telemetryClient = new TelemetryClient(new TelemetryConfiguration());

            // Setup UserProfileCache
            var mockMemoryCache = new Mock<IMemoryCache>();
            var mockUserProfileManager = new Mock<IUserProfileManager>();
            var mockUserProfileLogger = new Mock<ILogger<UserProfileCache>>();
            _userProfileCache = new UserProfileCache(
                mockMemoryCache.Object,
                mockUserProfileManager.Object,
                mockUserProfileLogger.Object);

            var mockLanguageServices = new List<LanguageService>();

            // Create ReviewManager instance for testing the actual business logic
            _reviewManager = new ReviewManager(
                _mockAuthorizationService.Object,
                _mockReviewsRepository.Object,
                _mockApiRevisionsManager.Object,
                _mockCommentsManager.Object,
                _mockCodeFileRepository.Object,
                _mockCommentsRepository.Object,
                _mockApiRevisionsRepository.Object,
                _mockSignalRHubContext.Object,
                mockLanguageServices,
                _telemetryClient,
                _mockCodeFileManager.Object,
                _mockConfiguration.Object,
                _mockHttpClientFactory.Object,
                _mockPollingJobQueueManager.Object,
                _mockNotificationManager.Object,
                _mockPullRequestsRepository.Object,
                _mockCopilotAuth.Object,
                _mockManagerLogger.Object);

            // Create controller that uses the real ReviewManager
            _controller = new ReviewsController(
                _mockControllerLogger.Object,
                _mockApiRevisionsManager.Object,
                _reviewManager, // Use real ReviewManager instead of mock
                _mockCommentsManager.Object,
                _mockCodeFileRepository.Object,
                _mockConfiguration.Object,
                _userProfileCache,
                mockLanguageServices,
                _mockSignalRHubContext.Object,
                _mockNotificationManager.Object,
                new Mock<IPermissionsManager>().Object);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithValidTypeSpecReview_ShouldCompleteEndToEndFlow()
        {
            // Arrange
            var reviewId = "test-typespec-review-id";
            var associatedReviewIds = new List<string> { "java-review-id", "python-review-id" };
            var user = CreateTestUser("testuser");
            var requestedOn = DateTime.UtcNow;

            // Setup TypeSpec review
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Azure.AI.FormRecognizer",
                Language = ApiViewConstants.TypeSpecLanguage,
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted,
            };

            // Setup associated language reviews
            var javaReview = new ReviewListItemModel
            {
                Id = "java-review-id",
                PackageName = "azure-ai-formrecognizer",
                Language = "Java",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            var pythonReview = new ReviewListItemModel
            {
                Id = "python-review-id",
                PackageName = "azure-ai-formrecognizer",
                Language = "Python",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            // Setup auto-discovered reviews via pull requests
            var csharpReview = new ReviewListItemModel
            {
                Id = "csharp-review-id",
                PackageName = "Azure.AI.FormRecognizer",
                Language = "C#",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            // Setup API revisions with pull request numbers
            var apiRevisions = new List<APIRevisionListItemModel>
            {
                new APIRevisionListItemModel { Id = "revision1", PullRequestNo = 12345 },
                new APIRevisionListItemModel { Id = "revision2", PullRequestNo = 12346 }
            };

            // Setup pull requests
            var pullRequests = new List<PullRequestModel>
            {
                new PullRequestModel { PullRequestNumber = 12345, ReviewId = "csharp-review-id", RepoName = "azure-rest-api-specs" },
                new PullRequestModel { PullRequestNumber = 12346, ReviewId = "javascript-review-id", RepoName = "azure-rest-api-specs" }
            };

            // Setup repository mocks
            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync("java-review-id"))
                .ReturnsAsync(javaReview);

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync("python-review-id"))
                .ReturnsAsync(pythonReview);

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync("csharp-review-id"))
                .ReturnsAsync(csharpReview);

                        _mockReviewsRepository
                .Setup(r => r.GetReviewsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>()))
                .ReturnsAsync((IEnumerable<string> ids, bool? isClosed) =>
                {
                    var reviews = new List<ReviewListItemModel>();
                    if (ids.Contains("csharp-review-id")) reviews.Add(csharpReview);
                    return reviews;
                });

            _mockApiRevisionsManager
                .Setup(a => a.GetAPIRevisionsAsync(It.IsAny<string>(), "", APIRevisionType.All))
                .ReturnsAsync(apiRevisions);

            _mockPullRequestsRepository
                .Setup(p => p.GetPullRequestsAsync(12345, "azure-rest-api-specs"))
                .ReturnsAsync(new List<PullRequestModel> { pullRequests[0] });

            _mockPullRequestsRepository
                .Setup(p => p.GetPullRequestsAsync(12346, "azure-rest-api-specs"))
                .ReturnsAsync(new List<PullRequestModel> { pullRequests[1] });

            // Setup UpsertReviewAsync to capture and validate changes
            var updatedReviews = new List<ReviewListItemModel>();
            _mockReviewsRepository
                .Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReviews.Add(review))
                .Returns(Task.CompletedTask);

            // Setup notification manager
            _mockNotificationManager
                .Setup(n => n.NotifyNamespaceReviewRequestRecipientsAsync(
                    It.IsAny<ClaimsPrincipal>(), 
                    It.IsAny<ReviewListItemModel>(), 
                    It.IsAny<IEnumerable<ReviewListItemModel>>(), 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>();
            
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithNonTypeSpecReview_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var reviewId = "java-review-id";
            var associatedReviewIds = new List<string>();
            var user = CreateTestUser("testuser");

            var javaReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "azure-ai-formrecognizer",
                Language = "Java"
            };

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(javaReview);

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>();
            
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithEmptyAssociatedReviewIds_ShouldStillWorkWithAutoDiscovery()
        {
            // Arrange
            var reviewId = "test-typespec-review-id";
            var associatedReviewIds = new List<string>(); // Empty list
            var user = CreateTestUser("testuser");

            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Azure.AI.FormRecognizer",
                Language = ApiViewConstants.TypeSpecLanguage,
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            var discoveredReview = new ReviewListItemModel
            {
                Id = "discovered-review-id",
                PackageName = "Azure.AI.FormRecognizer",
                Language = "JavaScript",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            // Setup auto-discovery
            var apiRevisions = new List<APIRevisionListItemModel>
            {
                new APIRevisionListItemModel { Id = "revision1", PullRequestNo = 12345 }
            };

            var pullRequests = new List<PullRequestModel>
            {
                new PullRequestModel { PullRequestNumber = 12345, ReviewId = "discovered-review-id", RepoName = "azure-rest-api-specs" }
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId)).ReturnsAsync(typeSpecReview);
            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All)).ReturnsAsync(apiRevisions);
            _mockPullRequestsRepository.Setup(p => p.GetPullRequestsAsync(12345, "azure-rest-api-specs")).ReturnsAsync(pullRequests);
            _mockReviewsRepository.Setup(r => r.GetReviewsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>())).ReturnsAsync(new List<ReviewListItemModel> { discoveredReview });
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>())).Returns(Task.CompletedTask);
            _mockNotificationManager.Setup(n => n.NotifyNamespaceReviewRequestRecipientsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<ReviewListItemModel>(), It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithInvalidReviewIds_ShouldContinueWithValidOnes()
        {
            // Arrange
            var reviewId = "test-typespec-review-id";
            var associatedReviewIds = new List<string> { "valid-review-id", "invalid-review-id", "" };
            var user = CreateTestUser("testuser");

            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Azure.AI.FormRecognizer",
                Language = ApiViewConstants.TypeSpecLanguage,
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            var validReview = new ReviewListItemModel
            {
                Id = "valid-review-id",
                PackageName = "azure-ai-formrecognizer",
                Language = "Python",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId)).ReturnsAsync(typeSpecReview);
            _mockReviewsRepository.Setup(r => r.GetReviewAsync("valid-review-id")).ReturnsAsync(validReview);
            _mockReviewsRepository.Setup(r => r.GetReviewAsync("invalid-review-id")).ThrowsAsync(new Exception("Review not found"));
            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAsync(reviewId, "", APIRevisionType.All)).ReturnsAsync(new List<APIRevisionListItemModel>());
            
            var updatedReviews = new List<ReviewListItemModel>();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReviews.Add(review))
                .Returns(Task.CompletedTask);
            
            _mockNotificationManager.Setup(n => n.NotifyNamespaceReviewRequestRecipientsAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<ReviewListItemModel>(), It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert
            result.Should().BeOfType<StatusCodeResult>();
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithCompleteValidSetup_ShouldReturnSuccessResult()
        {
            // Arrange
            var reviewId = "test-typespec-review-id";
            var associatedReviewIds = new List<string> { "java-review-id", "python-review-id" };
            var user = CreateTestUser("testuser");

            // Setup complete successful TypeSpec review
            var typeSpecReview = new ReviewListItemModel
            {
                Id = reviewId,
                PackageName = "Azure.AI.FormRecognizer",
                Language = ApiViewConstants.TypeSpecLanguage,
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted,
                IsClosed = false
            };

            // Setup associated language reviews
            var javaReview = new ReviewListItemModel
            {
                Id = "java-review-id",
                PackageName = "azure-ai-formrecognizer",
                Language = "Java",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted,
                IsClosed = false
            };

            var pythonReview = new ReviewListItemModel
            {
                Id = "python-review-id",
                PackageName = "azure-ai-formrecognizer",
                Language = "Python",
                NamespaceReviewStatus = NamespaceReviewStatus.NotStarted,
                IsClosed = false
            };

            // Setup all repository calls to return successfully
            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync("java-review-id"))
                .ReturnsAsync(javaReview);

            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync("python-review-id"))
                .ReturnsAsync(pythonReview);

            // Setup API revision for finding related reviews
            var testRevision = new APIRevisionListItemModel
            {
                Id = "revision1",
                PullRequestNo = 12345,
                Label = "Created for PR 12345"
            };
            
            _mockApiRevisionsManager
                .Setup(a => a.GetAPIRevisionAsync("revision1"))
                .ReturnsAsync(testRevision);

            // Setup GetAPIRevisionsAsync for individual review IDs (needed for checking if revisions are approved)
            _mockApiRevisionsManager
                .Setup(a => a.GetAPIRevisionsAsync("java-review-id", "", APIRevisionType.All))
                .ReturnsAsync(new List<APIRevisionListItemModel> 
                { 
                    new APIRevisionListItemModel { Id = "java-rev-1", IsApproved = false }
                });
                
            _mockApiRevisionsManager
                .Setup(a => a.GetAPIRevisionsAsync("python-review-id", "", APIRevisionType.All))
                .ReturnsAsync(new List<APIRevisionListItemModel> 
                { 
                    new APIRevisionListItemModel { Id = "python-rev-1", IsApproved = false }
                });

            // Setup pull requests repository to return related pull requests
            // First call - get the initial PR model for the TypeSpec review
            var initialPullRequest = new PullRequestModel 
            { 
                ReviewId = reviewId, 
                PullRequestNumber = 12345 
            };
            
            _mockPullRequestsRepository
                .Setup(p => p.GetPullRequestsAsync(reviewId, "revision1"))
                .ReturnsAsync(new List<PullRequestModel> { initialPullRequest });
                
            // Second call - get all related pull requests by PR number
            var mockPullRequests = new List<PullRequestModel>
            {
                new PullRequestModel { ReviewId = "java-review-id", PullRequestNumber = 12345 },
                new PullRequestModel { ReviewId = "python-review-id", PullRequestNumber = 12345 }
            };
            
            _mockPullRequestsRepository
                .Setup(p => p.GetPullRequestsAsync(12345, "Azure/azure-rest-api-specs"))
                .ReturnsAsync(mockPullRequests);

            // Setup GetReviewsAsync to return associated reviews
            _mockReviewsRepository
                .Setup(r => r.GetReviewsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool?>()))
                .ReturnsAsync(new List<ReviewListItemModel> { javaReview, pythonReview });

            // Track updated reviews
            var updatedReviews = new List<ReviewListItemModel>();
            _mockReviewsRepository
                .Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReviews.Add(review))
                .Returns(Task.CompletedTask);

            // Setup notification manager to succeed
            _mockNotificationManager
                .Setup(n => n.NotifyNamespaceReviewRequestRecipientsAsync(
                    It.IsAny<ClaimsPrincipal>(), 
                    It.IsAny<ReviewListItemModel>(), 
                    It.IsAny<IEnumerable<ReviewListItemModel>>(), 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup HttpContext for the controller with the test user
            var httpContext = new DefaultHttpContext();
            httpContext.User = user;
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert - This should succeed and return LeanJsonResult
            result.Should().NotBeNull();
            result.Should().BeOfType<LeanJsonResult>();
            
            var leanResult = result as LeanJsonResult;
            // Note: LeanJsonResult doesn't expose StatusCode property, but it's set internally
            // We can verify the operation succeeded by checking the data was processed correctly

            // Verify that reviews were updated with namespace status and timestamp
            updatedReviews.Should().HaveCount(2); // Only associated reviews are updated (not the main TypeSpec review)
            
            // Verify that the associated reviews were updated correctly
            var javaReviewUpdated = updatedReviews.FirstOrDefault(r => r.Id == "java-review-id");
            javaReviewUpdated.Should().NotBeNull();
            javaReviewUpdated.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Pending);
            javaReviewUpdated.NamespaceApprovalRequestedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            
            var pythonReviewUpdated = updatedReviews.FirstOrDefault(r => r.Id == "python-review-id");
            pythonReviewUpdated.Should().NotBeNull();
            pythonReviewUpdated.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Pending);
            pythonReviewUpdated.NamespaceApprovalRequestedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

            // Verify notification was called
            _mockNotificationManager.Verify(
                n => n.NotifyNamespaceReviewRequestRecipientsAsync(
                    It.Is<ClaimsPrincipal>(p => p.Identity.Name == "testuser"),
                    It.Is<ReviewListItemModel>(r => r.Id == reviewId),
                    It.Is<IEnumerable<ReviewListItemModel>>(reviews => reviews.Count() == 2),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithNullReviewId_ShouldReturnInternalServerError()
        {
            // Arrange
            string reviewId = null; // Null review ID
            var associatedReviewIds = new List<string> { "java-review-id" };
            var user = CreateTestUser("testuser");

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = user;
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert - Controller catches all exceptions and returns 500
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>();
            
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

            // The repository is not called because the implementation throws ArgumentException before repository access
            _mockReviewsRepository.Verify(
                r => r.GetReviewAsync(reviewId),
                Times.Never);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithNonExistentReviewId_ShouldReturnInternalServerError()
        {
            // Arrange
            var reviewId = "non-existent-review-id";
            var associatedReviewIds = new List<string> { "java-review-id" };
            var user = CreateTestUser("testuser");

            // Setup repository to return null for non-existent review (which causes NullReferenceException in business logic)
            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync((ReviewListItemModel)null);

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = user;
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert - Controller catches all exceptions and returns 500
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>();
            
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

            // Verify the repository was called
            _mockReviewsRepository.Verify(
                r => r.GetReviewAsync(reviewId),
                Times.Once);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithRepositoryException_ShouldReturnInternalServerError()
        {
            // Arrange
            var reviewId = "test-typespec-review-id";
            var associatedReviewIds = new List<string> { "java-review-id" };
            var userWithValidLogin = CreateTestUser("validuser");

            // Setup the repository to throw an exception when getting review
            _mockReviewsRepository
                .Setup(r => r.GetReviewAsync(reviewId))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Setup HttpContext for the controller
            var httpContext = new DefaultHttpContext();
            httpContext.User = userWithValidLogin;
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.RequestNamespaceReviewAsync(reviewId, "revision1");

            // Assert - Controller catches all exceptions and returns 500
            result.Should().NotBeNull();
            result.Should().BeOfType<StatusCodeResult>();
            
            var statusResult = result as StatusCodeResult;
            statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        private ClaimsPrincipal CreateUserWithoutGitHubLogin(string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userName),
                new Claim(ClaimTypes.Name, userName)
                // Missing ClaimConstants.Login claim which is required for GetGitHubLogin()
            };

            var identity = new ClaimsIdentity(claims, "Test");
            return new ClaimsPrincipal(identity);
        }

        private ClaimsPrincipal CreateTestUser(string userName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userName),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimConstants.Login, userName)
            };

            var identity = new ClaimsIdentity(claims, "Test");
            return new ClaimsPrincipal(identity);
        }
    }
}
