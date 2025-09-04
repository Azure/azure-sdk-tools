using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Net.Http;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    /// <summary>
    /// Comprehensive end-to-end tests for Namespace Approval functionality
    /// Tests the complete approval workflow for namespace reviews including manual approval,
    /// approval verification, and notification processes
    /// </summary>
    public class NamespaceApprovalTests
    {
        private readonly Mock<ICosmosReviewRepository> _mockReviewsRepository;
        private readonly Mock<INotificationManager> _mockNotificationManager;
        private readonly Mock<ICosmosPullRequestsRepository> _mockPullRequestsRepository;
        private readonly Mock<IAuthorizationService> _mockAuthorizationService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<ReviewManager>> _mockLogger;
        private readonly TelemetryClient _telemetryClient;
        private readonly ReviewManager _reviewManager;
        private readonly ClaimsPrincipal _testUser;
        private readonly DateTime _testTimestamp;

        public NamespaceApprovalTests()
        {
            _mockReviewsRepository = new Mock<ICosmosReviewRepository>();
            _mockPullRequestsRepository = new Mock<ICosmosPullRequestsRepository>();
            _mockNotificationManager = new Mock<INotificationManager>();
            _mockAuthorizationService = new Mock<IAuthorizationService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ReviewManager>>();
            _telemetryClient = new TelemetryClient(new TelemetryConfiguration());

            // Create test user with proper GitHub login claim
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "testapprover"),
                new Claim(ClaimTypes.Name, "Test Approver"),
                new Claim(ClaimConstants.Login, "testapprover")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            _testUser = new ClaimsPrincipal(identity);

            // Setup authorization to always pass for approved reviewers
            _mockAuthorizationService.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
                .ReturnsAsync(AuthorizationResult.Success());

            // Setup configuration to enable namespace review functionality
            _mockConfiguration.Setup(c => c["EnableNamespaceReview"]).Returns("true");

            // Create ReviewManager instance with all required parameters
            var mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            var mockCommentsManager = new Mock<ICommentsManager>();
            var mockCodeFileRepository = new Mock<IBlobCodeFileRepository>();
            var mockCommentsRepository = new Mock<ICosmosCommentsRepository>();
            var mockApiRevisionsRepository = new Mock<ICosmosAPIRevisionsRepository>();
            var mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
            var mockLanguageServices = new Mock<IEnumerable<LanguageService>>();
            var mockCodeFileManager = new Mock<ICodeFileManager>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var mockPollingJobQueueManager = new Mock<IPollingJobQueueManager>();

            _reviewManager = new ReviewManager(
                _mockAuthorizationService.Object,
                _mockReviewsRepository.Object,
                mockApiRevisionsManager.Object,
                mockCommentsManager.Object,
                mockCodeFileRepository.Object,
                mockCommentsRepository.Object,
                mockApiRevisionsRepository.Object,
                mockSignalRHubContext.Object,
                mockLanguageServices.Object,
                _telemetryClient,
                mockCodeFileManager.Object,
                _mockConfiguration.Object,
                mockHttpClientFactory.Object,
                mockPollingJobQueueManager.Object,
                _mockNotificationManager.Object,
                _mockPullRequestsRepository.Object,
                _mockLogger.Object);

            _testTimestamp = DateTime.UtcNow;
        }

        [Fact]
        public async Task ApproveNamespaceReview_WithValidTypeSpecReview_ShouldApproveSuccessfully()
        {
            // Arrange - Create a TypeSpec review with pending namespace status
            var reviewId = "typespec-review-123";
            var typeSpecReview = CreateReview(reviewId, ApiViewConstants.TypeSpecLanguage, "Azure.AI.DocumentIntelligence", 
                NamespaceReviewStatus.Pending, "original-requester", _testTimestamp);

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);

            var updatedReview = new ReviewListItemModel();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReview = review)
                .Returns(Task.CompletedTask);

            // Act - Approve the namespace review
            var result = await _reviewManager.ToggleReviewApprovalAsync(_testUser, reviewId, reviewId, "Namespace approved");

            // Assert - Verify the review was approved
            result.Should().NotBeNull();
            result.IsApproved.Should().BeTrue();
            
            // Verify the review was updated with approval status
            updatedReview.Should().NotBeNull();
            updatedReview.IsApproved.Should().BeTrue();
            updatedReview.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Approved);
        }

        [Fact]
        public async Task ApproveNamespaceReview_WithSDKLanguageReview_ShouldApproveIndividualReview()
        {
            // Arrange - Create an SDK language review with pending namespace status
            var reviewId = "csharp-review-456";
            var csharpReview = CreateReview(reviewId, "C#", "Azure.AI.DocumentIntelligence", 
                NamespaceReviewStatus.Pending, "original-requester", _testTimestamp);

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(csharpReview);

            var updatedReview = new ReviewListItemModel();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReview = review)
                .Returns(Task.CompletedTask);

            // Act - Approve the C# SDK review
            var result = await _reviewManager.ToggleReviewApprovalAsync(_testUser, reviewId, reviewId, "C# SDK approved");

            // Assert - Verify the review was approved
            result.Should().NotBeNull();
            result.IsApproved.Should().BeTrue();
            
            // Verify the review maintains namespace pending status (individual approval)
            updatedReview.Should().NotBeNull();
            updatedReview.IsApproved.Should().BeTrue();
            updatedReview.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Pending);
        }

        [Fact]
        public async Task GetPendingNamespaceApprovals_WithMultipleReviews_ShouldReturnCorrectReviews()
        {
            // Arrange - Create a mix of reviews with different namespace statuses
            var pendingReviews = new List<ReviewListItemModel>
            {
                CreateReview("pending-1", "C#", "Azure.AI.FormRecognizer", NamespaceReviewStatus.Pending, "user1", _testTimestamp),
                CreateReview("pending-2", "Java", "Azure.AI.FormRecognizer", NamespaceReviewStatus.Pending, "user2", _testTimestamp),
                CreateReview("approved-1", "Python", "Azure.AI.FormRecognizer", NamespaceReviewStatus.Approved, "user3", _testTimestamp),
                CreateReview("not-started-1", "JavaScript", "Azure.AI.FormRecognizer", NamespaceReviewStatus.NotStarted, "user4", _testTimestamp)
            };

            _mockReviewsRepository.Setup(r => r.GetPendingNamespaceApprovalReviewsAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(pendingReviews.Where(r => r.NamespaceReviewStatus == NamespaceReviewStatus.Pending).ToList());

            // Act - Get pending namespace approvals
            var result = await _reviewManager.GetPendingNamespaceApprovalsBatchAsync(10);

            // Assert - Should return only pending reviews
            result.Should().HaveCount(2);
            result.Should().OnlyContain(r => r.NamespaceReviewStatus == NamespaceReviewStatus.Pending);
            result.Should().Contain(r => r.Id == "pending-1");
            result.Should().Contain(r => r.Id == "pending-2");
        }

        [Fact]
        public async Task RequestNamespaceReview_WithValidTypeSpecReview_ShouldCreateNamespaceRequest()
        {
            // Arrange - Create a TypeSpec review not yet requested for namespace approval
            var reviewId = "typespec-review-789";
            var associatedReviewIds = new List<string> { "csharp-review-101", "java-review-102" };
            
            var typeSpecReview = CreateReview(reviewId, ApiViewConstants.TypeSpecLanguage, "Azure.AI.TextAnalytics", 
                NamespaceReviewStatus.NotStarted, null, null);

            var csharpReview = CreateReview("csharp-review-101", "C#", "Azure.AI.TextAnalytics", 
                NamespaceReviewStatus.NotStarted, null, null);
            
            var javaReview = CreateReview("java-review-102", "Java", "Azure.AI.TextAnalytics", 
                NamespaceReviewStatus.NotStarted, null, null);

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(typeSpecReview);
            
            _mockReviewsRepository.Setup(r => r.GetReviewAsync("csharp-review-101"))
                .ReturnsAsync(csharpReview);
            
            _mockReviewsRepository.Setup(r => r.GetReviewAsync("java-review-102"))
                .ReturnsAsync(javaReview);

            // Setup empty pull request relationships for simplicity
            _mockPullRequestsRepository.Setup(pr => pr.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<PullRequestModel>());

            var updatedReviews = new List<ReviewListItemModel>();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReviews.Add(review))
                .Returns(Task.CompletedTask);

            _mockNotificationManager.Setup(n => n.NotifyApproversOnNamespaceReviewRequest(
                    It.IsAny<ClaimsPrincipal>(), It.IsAny<ReviewListItemModel>(), It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act - Request namespace review
            var result = await _reviewManager.RequestNamespaceReviewAsync(_testUser, reviewId, associatedReviewIds);

            // Assert - Verify namespace review was requested
            result.Should().NotBeNull();
            result.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Pending);
            result.NamespaceApprovalRequestedBy.Should().Be("testapprover");
            result.NamespaceApprovalRequestedOn.Should().BeCloseTo(_testTimestamp, TimeSpan.FromMinutes(1));

            // Verify all associated reviews were updated
            updatedReviews.Should().HaveCount(3); // TypeSpec + 2 associated reviews
            updatedReviews.Should().OnlyContain(r => r.NamespaceReviewStatus == NamespaceReviewStatus.Pending);

            // Verify notification was sent
            _mockNotificationManager.Verify(n => n.NotifyApproversOnNamespaceReviewRequest(
                It.Is<ClaimsPrincipal>(p => p.Identity.Name == "testapprover"),
                It.Is<ReviewListItemModel>(r => r.Id == reviewId),
                It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RequestNamespaceReview_WithNonTypeSpecReview_ShouldThrowException()
        {
            // Arrange - Create a non-TypeSpec review
            var reviewId = "csharp-review-999";
            var csharpReview = CreateReview(reviewId, "C#", "Azure.AI.DocumentIntelligence", 
                NamespaceReviewStatus.NotStarted, null, null);

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(csharpReview);

            // Act & Assert - Should throw exception for non-TypeSpec reviews
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _reviewManager.RequestNamespaceReviewAsync(_testUser, reviewId, new List<string>()));

            exception.Message.Should().Contain("Namespace review can only be requested for TypeSpec reviews");
        }

        [Fact]
        public async Task VerifyNamespaceApprovalWorkflow_EndToEnd_ShouldCompleteSuccessfully()
        {
            // Arrange - Create a complete namespace approval scenario
            var typeSpecReviewId = "typespec-review-e2e";
            var csharpReviewId = "csharp-review-e2e";
            var javaReviewId = "java-review-e2e";
            
            var typeSpecReview = CreateReview(typeSpecReviewId, ApiViewConstants.TypeSpecLanguage, "Azure.AI.Language", 
                NamespaceReviewStatus.NotStarted, null, null);
            
            var csharpReview = CreateReview(csharpReviewId, "C#", "Azure.AI.Language", 
                NamespaceReviewStatus.NotStarted, null, null);
            
            var javaReview = CreateReview(javaReviewId, "Java", "Azure.AI.Language", 
                NamespaceReviewStatus.NotStarted, null, null);

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(typeSpecReviewId))
                .ReturnsAsync(typeSpecReview);
            _mockReviewsRepository.Setup(r => r.GetReviewAsync(csharpReviewId))
                .ReturnsAsync(csharpReview);
            _mockReviewsRepository.Setup(r => r.GetReviewAsync(javaReviewId))
                .ReturnsAsync(javaReview);

            var updatedReviews = new List<ReviewListItemModel>();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReviews.Add(review))
                .Returns(Task.CompletedTask);

            _mockNotificationManager.Setup(n => n.NotifyApproversOnNamespaceReviewRequest(
                    It.IsAny<ClaimsPrincipal>(), It.IsAny<ReviewListItemModel>(), It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup empty pull request relationships
            _mockPullRequestsRepository.Setup(pr => pr.GetPullRequestsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<PullRequestModel>());

            // Act 1 - Request namespace review
            var requestResult = await _reviewManager.RequestNamespaceReviewAsync(_testUser, typeSpecReviewId, 
                new List<string> { csharpReviewId, javaReviewId });

            // Act 2 - Approve individual SDK reviews
            await _reviewManager.ToggleReviewApprovalAsync(_testUser, csharpReviewId, csharpReviewId, "C# approved");
            await _reviewManager.ToggleReviewApprovalAsync(_testUser, javaReviewId, javaReviewId, "Java approved");

            // Act 3 - Approve the TypeSpec namespace review
            var finalResult = await _reviewManager.ToggleReviewApprovalAsync(_testUser, typeSpecReviewId, typeSpecReviewId, "Namespace approved");

            // Assert - Verify the complete workflow
            requestResult.Should().NotBeNull();
            requestResult.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Pending);

            finalResult.Should().NotBeNull();
            finalResult.IsApproved.Should().BeTrue();

            // Verify all reviews were updated appropriately
            updatedReviews.Should().HaveCountGreaterThan(3); // Multiple updates during the workflow
            
            var finalTypeSpecUpdate = updatedReviews.Last(r => r.Id == typeSpecReviewId);
            finalTypeSpecUpdate.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Approved);
            finalTypeSpecUpdate.IsApproved.Should().BeTrue();

            // Verify notification was sent for initial request
            _mockNotificationManager.Verify(n => n.NotifyApproversOnNamespaceReviewRequest(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<ReviewListItemModel>(), It.IsAny<IEnumerable<ReviewListItemModel>>(), It.IsAny<string>()), 
                Times.Once);
        }

        [Fact]
        public async Task NamespaceApprovalWithMissingData_ShouldHandleGracefully()
        {
            // Arrange - Create reviews with incomplete namespace data
            var reviewId = "incomplete-review";
            var incompleteReview = new ReviewListItemModel
            {
                Id = reviewId,
                Language = ApiViewConstants.TypeSpecLanguage,
                PackageName = "Azure.AI.Incomplete",
                NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                NamespaceApprovalRequestedBy = null, // Missing requester
                NamespaceApprovalRequestedOn = null  // Missing timestamp
            };

            _mockReviewsRepository.Setup(r => r.GetReviewAsync(reviewId))
                .ReturnsAsync(incompleteReview);

            var updatedReview = new ReviewListItemModel();
            _mockReviewsRepository.Setup(r => r.UpsertReviewAsync(It.IsAny<ReviewListItemModel>()))
                .Callback<ReviewListItemModel>(review => updatedReview = review)
                .Returns(Task.CompletedTask);

            // Act - Approve the review despite missing data
            var result = await _reviewManager.ToggleReviewApprovalAsync(_testUser, reviewId, reviewId, "Approved despite incomplete data");

            // Assert - Should still work and approve the review
            result.Should().NotBeNull();
            result.IsApproved.Should().BeTrue();
            
            updatedReview.Should().NotBeNull();
            updatedReview.IsApproved.Should().BeTrue();
            updatedReview.NamespaceReviewStatus.Should().Be(NamespaceReviewStatus.Approved);
        }

        #region Helper Methods

        private ReviewListItemModel CreateReview(string id, string language, string packageName, 
            NamespaceReviewStatus namespaceStatus, string requestedBy, DateTime? requestedOn, bool isApproved = false)
        {
            return new ReviewListItemModel
            {
                Id = id,
                Language = language,
                PackageName = packageName,
                NamespaceReviewStatus = namespaceStatus,
                NamespaceApprovalRequestedBy = requestedBy,
                NamespaceApprovalRequestedOn = requestedOn,
                IsApproved = isApproved,
                IsClosed = false,
                IsDeleted = false,
                CreatedBy = "test-user",
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                LastUpdatedOn = DateTime.UtcNow
            };
        }

        #endregion
    }
}
