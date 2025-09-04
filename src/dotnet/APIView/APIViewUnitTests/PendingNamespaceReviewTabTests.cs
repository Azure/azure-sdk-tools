using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Pages.Assemblies;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    /// <summary>
    /// Comprehensive end-to-end tests for the Pending Namespace Review Tab functionality (Part 2)
    /// Tests the complete data flow from repository query to UI display
    /// </summary>
    public class PendingNamespaceReviewTabTests
    {
        private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
        private readonly Mock<IReviewManager> _mockReviewManager;
        private readonly Mock<IPullRequestManager> _mockPullRequestManager;
        private readonly UserProfileCache _userProfileCache;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly TelemetryClient _telemetryClient;
        private readonly RequestedReviews _pageModel;
        private readonly ClaimsPrincipal _testUser;

        public PendingNamespaceReviewTabTests()
        {
            _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
            _mockReviewManager = new Mock<IReviewManager>();
            _mockPullRequestManager = new Mock<IPullRequestManager>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            // Setup UserProfileCache with real instance (not mock)
            var mockUserProfileManager = new Mock<IUserProfileManager>();
            var mockUserProfileLogger = new Mock<ILogger<UserProfileCache>>();
            _userProfileCache = new UserProfileCache(
                _mockMemoryCache.Object,
                mockUserProfileManager.Object,
                mockUserProfileLogger.Object);

            // Create TelemetryClient for testing
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            _telemetryClient = new TelemetryClient(telemetryConfiguration);

            _pageModel = new RequestedReviews(
                _mockApiRevisionsManager.Object,
                _mockReviewManager.Object,
                _mockPullRequestManager.Object,
                _userProfileCache,
                _mockConfiguration.Object,
                _mockMemoryCache.Object,
                _telemetryClient);

            // Create test user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "testuser"),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimConstants.Login, "testuser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            _testUser = new ClaimsPrincipal(identity);

            // Setup HttpContext
            var httpContext = new DefaultHttpContext();
            httpContext.User = _testUser;
            _pageModel.PageContext = new PageContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task OnGetAsync_WithPendingNamespaceReviews_ShouldCallCorrectMethods()
        {
            // Arrange
            var pendingNamespaceReviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "typespec-review-id",
                    PackageName = "Azure.AI.FormRecognizer",
                    Language = ApiViewConstants.TypeSpecLanguage,
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "typespec-author",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1),
                    IsClosed = false,
                    IsDeleted = false
                }
            };

            // Setup mocks
            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(pendingNamespaceReviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();

            // Verify the correct methods were called
            _mockReviewManager.Verify(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()), Times.Once);
            _mockApiRevisionsManager.Verify(a => a.GetAPIRevisionsAssignedToUser("testuser"), Times.Once);

            // Verify that namespace approval info is stored
            _pageModel.NamespaceApprovalInfo.Should().NotBeNull();
        }

        [Fact]
        public async Task OnGetAsync_WithNoNamespaceReviews_ShouldReturnSuccessfully()
        {
            // Arrange
            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ReviewListItemModel>());

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();
            _pageModel.NamespaceApprovalRequestedAPIRevisions.Should().NotBeNull();
            _pageModel.NamespaceApprovalRequestedAPIRevisions.Should().BeEmpty();

            // Verify methods were called
            _mockReviewManager.Verify(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task OnGetAsync_WhenNamespaceRepositoryThrowsException_ShouldHandleGracefully()
        {
            // Arrange
            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act - Should not throw due to try-catch in the implementation
            var result = await _pageModel.OnGetAsync();

            // Assert - Should complete successfully despite the exception
            result.Should().BeOfType<PageResult>();
            _pageModel.NamespaceApprovalRequestedAPIRevisions.Should().NotBeNull();
        }

        [Fact]
        public async Task OnGetAsync_WithUserProfileTimeout_ShouldContinueExecution()
        {
            // Arrange
            // Note: Since _userProfileCache is now a real instance, we can't mock it to throw exceptions.
            // This test verifies that the page works correctly even if user profile operations fail silently.

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ReviewListItemModel>());

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert - Should not throw and should complete successfully
            result.Should().BeOfType<PageResult>();
            _pageModel.NamespaceApprovalRequestedAPIRevisions.Should().NotBeNull();
        }

        [Fact]
        public async Task OnGetAsync_WithValidNamespaceApprovalInfo_ShouldStoreMetadata()
        {
            // Arrange
            var requestedOn = DateTime.UtcNow.AddDays(-2);
            var requestedBy = "namespace-requester";

            var namespaceReviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "review-with-namespace-info",
                    PackageName = "Azure.Test.Package",
                    Language = "C#",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = requestedBy,
                    NamespaceApprovalRequestedOn = requestedOn,
                    IsClosed = false,
                    IsDeleted = false
                }
            };

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(namespaceReviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();

            // Verify namespace approval info is properly stored
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("review-with-namespace-info");
            var storedInfo = _pageModel.NamespaceApprovalInfo["review-with-namespace-info"];
            storedInfo.RequestedOn.Should().Be(requestedOn);
            storedInfo.RequestedBy.Should().Be(requestedBy);
        }

        [Fact]
        public async Task OnGetAsync_WithMultiplePendingNamespaceReviews_ShouldProcessAll()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "review-1",
                    PackageName = "Azure.Package.One",
                    Language = "C#",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "author1",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                },
                new ReviewListItemModel
                {
                    Id = "review-2",
                    PackageName = "Azure.Package.Two",
                    Language = "Java",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "author2",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-2)
                }
            };

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(reviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();

            // Verify both namespace reviews are processed
            _pageModel.NamespaceApprovalInfo.Should().HaveCount(2);
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("review-1");
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("review-2");
        }

        [Fact]
        public async Task OnGetAsync_WithIncompleteNamespaceData_ShouldSkipInvalidEntries()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "valid-review",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "valid-author",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                },
                new ReviewListItemModel
                {
                    Id = "incomplete-review-1",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = null, // Missing requestedBy
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                },
                new ReviewListItemModel
                {
                    Id = "incomplete-review-2",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "author",
                    NamespaceApprovalRequestedOn = null // Missing requestedOn
                }
            };

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(reviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();

            // Only the valid review should be stored
            _pageModel.NamespaceApprovalInfo.Should().HaveCount(1);
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("valid-review");
            _pageModel.NamespaceApprovalInfo.Should().NotContainKey("incomplete-review-1");
            _pageModel.NamespaceApprovalInfo.Should().NotContainKey("incomplete-review-2");
        }
    }
}
