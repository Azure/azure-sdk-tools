using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.Hubs;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Pages.Assemblies;
using APIViewWeb.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class IndexPageModelTests
{
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ICodeFileManager> _mockCodeFileManager;
    private readonly Mock<IReviewManager> _mockReviewManager;
    private readonly Mock<IHubContext<SignalRHub>> _mockSignalRHubContext;
    private readonly IndexPageModel _pageModel;
    private readonly ClaimsPrincipal _testUser;
    private readonly UserProfileCache _userProfileCache;

    public IndexPageModelTests()
    {
        _mockReviewManager = new Mock<IReviewManager>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockSignalRHubContext = new Mock<IHubContext<SignalRHub>>();
        _mockCodeFileManager = new Mock<ICodeFileManager>();

        Mock<IMemoryCache> mockMemoryCache = new();
        Mock<IUserProfileManager> mockUserProfileManager = new();
        Mock<ILogger<UserProfileCache>> mockUserProfileLogger = new();
        _userProfileCache = new UserProfileCache(
            mockMemoryCache.Object,
            mockUserProfileManager.Object,
            mockUserProfileLogger.Object);

        _pageModel = new IndexPageModel(
            _mockReviewManager.Object,
            _mockApiRevisionsManager.Object,
            mockUserProfileManager.Object,
            _userProfileCache,
            _mockSignalRHubContext.Object,
            _mockCodeFileManager.Object);

        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, "testuser"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimConstants.Login, "testuser")
        };
        ClaimsIdentity identity = new(claims, "Test");
        _testUser = new ClaimsPrincipal(identity);

        DefaultHttpContext httpContext = new();
        httpContext.User = _testUser;
        _pageModel.PageContext = new PageContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task OnPostUploadAsync_WithPackageName_UsesExistingReview()
    {
        // Arrange
        ReviewListItemModel existingReview = new()
        {
            Id = "existing-review-id", PackageName = "azure-core", Language = "Python"
        };

        APIRevisionListItemModel newApiRevision = new() { Id = "new-revision-id", ReviewId = existingReview.Id };

        _pageModel.Upload = new UploadModel
        {
            Language = "Python", PackageName = "azure-core", FilePath = "/path/to/file.json"
        };
        _pageModel.Label = "Test Label";

        _mockReviewManager.Setup(m => m.GetReviewAsync("Python", "azure-core", false))
            .ReturnsAsync(existingReview);

        _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                existingReview,
                null,
                "/path/to/file.json",
                "Python",
                "Test Label"))
            .ReturnsAsync(newApiRevision);

        IActionResult result = await _pageModel.OnPostUploadAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        RedirectToPageResult redirectResult = result as RedirectToPageResult;
        redirectResult!.PageName.Should().Be("Review");
        redirectResult.RouteValues!["id"].Should().Be(existingReview.Id);
        redirectResult.RouteValues["revisionId"].Should().Be(newApiRevision.Id);

        _mockReviewManager.Verify(m => m.GetReviewAsync("Python", "azure-core", false), Times.Once);

        // Verify GetOrCreateReview was NOT called since we found an existing review
        _mockReviewManager.Verify(m => m.GetOrCreateReview(
            It.IsAny<IFormFile>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task OnPostUploadAsync_WithoutPackageName_CreatesNewReview()
    {
        ReviewListItemModel createdReview = new()
        {
            Id = "new-review-id", PackageName = "new-package", Language = "Python"
        };

        APIRevisionListItemModel newApiRevision = new() { Id = "new-revision-id", ReviewId = createdReview.Id };

        _pageModel.Upload = new UploadModel
        {
            Language = "Python",
            PackageName = null,
            FilePath = "/path/to/file.json"
        };
        _pageModel.Label = "Test Label";

        _mockReviewManager.Setup(m => m.GetOrCreateReview(
                null,
                "/path/to/file.json",
                "Python",
                false))
            .ReturnsAsync(createdReview);

        _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                createdReview,
                null,
                "/path/to/file.json",
                "Python",
                "Test Label"))
            .ReturnsAsync(newApiRevision);

        IActionResult result = await _pageModel.OnPostUploadAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        RedirectToPageResult redirectResult = result as RedirectToPageResult;
        redirectResult!.PageName.Should().Be("Review");
        redirectResult.RouteValues!["id"].Should().Be(createdReview.Id);
        redirectResult.RouteValues["revisionId"].Should().Be(newApiRevision.Id);

        _mockReviewManager.Verify(m => m.GetReviewAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool?>()), Times.Never);

        _mockReviewManager.Verify(m => m.GetOrCreateReview(
            null,
            "/path/to/file.json",
            "Python",
            false), Times.Once);
    }

    [Fact]
    public async Task OnPostUploadAsync_WithPackageName_NotFound_CreatesNewReview()
    {
        ReviewListItemModel createdReview = new()
        {
            Id = "new-review-id", PackageName = "nonexistent-package", Language = "Python"
        };

        APIRevisionListItemModel newApiRevision = new() { Id = "new-revision-id", ReviewId = createdReview.Id };

        _pageModel.Upload = new UploadModel
        {
            Language = "Python", PackageName = "nonexistent-package", FilePath = "/path/to/file.json"
        };
        _pageModel.Label = "Test Label";

        // GetReviewAsync returns null (package not found)
        _mockReviewManager.Setup(m => m.GetReviewAsync("Python", "nonexistent-package", false))
            .ReturnsAsync((ReviewListItemModel)null);

        _mockReviewManager.Setup(m => m.GetOrCreateReview(
                null,
                "/path/to/file.json",
                "Python",
                false))
            .ReturnsAsync(createdReview);

        _mockApiRevisionsManager.Setup(m => m.CreateAPIRevisionAsync(
                It.IsAny<ClaimsPrincipal>(),
                createdReview,
                null,
                "/path/to/file.json",
                "Python",
                "Test Label"))
            .ReturnsAsync(newApiRevision);

        IActionResult result = await _pageModel.OnPostUploadAsync();

        result.Should().BeOfType<RedirectToPageResult>();
        RedirectToPageResult redirectResult = result as RedirectToPageResult;
        redirectResult!.PageName.Should().Be("Review");
        redirectResult.RouteValues!["id"].Should().Be(createdReview.Id);
        redirectResult.RouteValues["revisionId"].Should().Be(newApiRevision.Id);

        // Verify GetReviewAsync was called to look up the package
        _mockReviewManager.Verify(m => m.GetReviewAsync("Python", "nonexistent-package", false), Times.Once);

        // Verify GetOrCreateReview was called as fallback
        _mockReviewManager.Verify(m => m.GetOrCreateReview(
            null,
            "/path/to/file.json",
            "Python",
            false), Times.Once);
    }
}
