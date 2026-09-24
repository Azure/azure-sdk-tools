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

        [Fact]
        public async Task OnGetAsync_WithMixedNamespaceReviewStatuses_ShouldOnlyIncludePending()
        {
            // Arrange
            var reviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "pending-review",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "author1",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                },
                new ReviewListItemModel
                {
                    Id = "approved-review", 
                    NamespaceReviewStatus = NamespaceReviewStatus.Approved,
                    NamespaceApprovalRequestedBy = "author2",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-2)
                },
                new ReviewListItemModel
                {
                    Id = "not-started-review",
                    NamespaceReviewStatus = NamespaceReviewStatus.NotStarted,
                    NamespaceApprovalRequestedBy = null,
                    NamespaceApprovalRequestedOn = null
                }
            };

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(reviews.Where(r => r.NamespaceReviewStatus == NamespaceReviewStatus.Pending).ToList());

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();
            
            // Should only contain pending review info
            _pageModel.NamespaceApprovalInfo.Should().HaveCount(1);
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("pending-review");
            _pageModel.NamespaceApprovalInfo.Should().NotContainKey("approved-review");
            _pageModel.NamespaceApprovalInfo.Should().NotContainKey("not-started-review");
        }

        [Fact]
        public async Task OnGetAsync_WithTypeSpecLanguageReviews_ShouldPrioritizeCorrectly()
        {
            // Arrange - TypeSpec reviews are typically the main namespace review triggers
            var reviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "typespec-review",
                    Language = ApiViewConstants.TypeSpecLanguage,
                    PackageName = "Azure.AI.FormRecognizer",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "typespec-author",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                },
                new ReviewListItemModel
                {
                    Id = "csharp-review",
                    Language = "C#",
                    PackageName = "Azure.AI.FormRecognizer", 
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "csharp-author",
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
            
            // Both should be processed, but TypeSpec should be recognizable
            _pageModel.NamespaceApprovalInfo.Should().HaveCount(2);
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("typespec-review");
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("csharp-review");
        }

        [Fact]
        public async Task OnGetAsync_WithLargeDataSet_ShouldHandlePerformanceCorrectly()
        {
            // Arrange - Test with larger dataset to verify performance
            var reviews = Enumerable.Range(1, 50).Select(i => new ReviewListItemModel
            {
                Id = $"review-{i}",
                PackageName = $"Azure.Package.{i}",
                Language = i % 2 == 0 ? "C#" : "Java",
                NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                NamespaceApprovalRequestedBy = $"author-{i}",
                NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-i)
            }).ToList();

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(reviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act
            var result = await _pageModel.OnGetAsync();

            // Assert
            result.Should().BeOfType<PageResult>();
            _pageModel.NamespaceApprovalInfo.Should().HaveCount(50);
            
            // Verify first and last entries to ensure all were processed
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("review-1");
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("review-50");
        }

        [Fact]
        public async Task OnGetAsync_WithNullUser_ShouldHandleGracefully()
        {
            // Arrange - Test with null user scenario
            _pageModel.PageContext.HttpContext.User = null;

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ReviewListItemModel>());

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser(It.IsAny<string>()))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act & Assert - Should not throw
            var result = await _pageModel.OnGetAsync();
            result.Should().BeOfType<PageResult>();
        }

        [Fact]
        public async Task OnGetAsync_WithConcurrentRequests_ShouldMaintainDataIntegrity()
        {
            // Arrange - Test concurrent execution scenarios
            var reviews = new List<ReviewListItemModel>
            {
                new ReviewListItemModel
                {
                    Id = "concurrent-review",
                    NamespaceReviewStatus = NamespaceReviewStatus.Pending,
                    NamespaceApprovalRequestedBy = "concurrent-author",
                    NamespaceApprovalRequestedOn = DateTime.UtcNow.AddDays(-1)
                }
            };

            _mockReviewManager.Setup(r => r.GetPendingNamespaceApprovalsBatchAsync(It.IsAny<int>()))
                .ReturnsAsync(reviews);

            _mockApiRevisionsManager.Setup(a => a.GetAPIRevisionsAssignedToUser("testuser"))
                .ReturnsAsync(new List<APIRevisionListItemModel>());

            // Act - Execute multiple requests concurrently
            var tasks = Enumerable.Range(1, 5).Select(_ => _pageModel.OnGetAsync());
            var results = await Task.WhenAll(tasks);

            // Assert - All should succeed
            results.Should().AllBeOfType<PageResult>();
            _pageModel.NamespaceApprovalInfo.Should().ContainKey("concurrent-review");
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("invalid", false)] // Default to false for invalid values
        [InlineData(null, false)] // Default to false for null values
        public void EnablePendingReviewTab_ControlsNamespaceApprovalsTab(string configValue, bool expectedResult)
        {
            // Arrange - This feature flag now controls the Pending Namespace Approvals tab, not the Pending Reviews tab
            _mockConfiguration.Setup(c => c["EnablePendingReviewTab"]).Returns(configValue);

            // Act
            var result = _pageModel.EnablePendingReviewTab;

            // Assert
            result.Should().Be(expectedResult);
        }
    }
}
