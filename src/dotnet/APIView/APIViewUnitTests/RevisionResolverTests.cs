using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class RevisionResolverTests
{
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ILogger<RevisionResolver>> _mockLogger;
    private readonly Mock<IReviewManager> _mockReviewManager;
    private readonly RevisionResolver _resolver;

    public RevisionResolverTests()
    {
        _mockReviewManager = new Mock<IReviewManager>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockLogger = new Mock<ILogger<RevisionResolver>>();

        _resolver = new RevisionResolver(
            _mockReviewManager.Object,
            _mockApiRevisionsManager.Object,
            _mockLogger.Object);
    }

    #region ResolveByPackageAsync Tests

    [Fact]
    public async Task ResolveByPackageAsync_WithValidPackageAndLanguage_ReturnsResult()
    {
        string packageName = "Azure.Storage.Blobs";
        string language = "C#";
        ReviewListItemModel mockReview = CreateMockReview("review123", packageName, language);
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision123", "review123", "1.0.0");

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(language, packageName, null))
            .ReturnsAsync(mockReview);

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(mockReview.Id, null, APIRevisionType.Automatic))
            .ReturnsAsync(mockRevision);

        RevisionResolveResult result = await _resolver.ResolveByPackageAsync(packageName, language);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision123", result.RevisionId);
    }

    [Fact]
    public async Task ResolveByPackageAsync_WithVersion_FindsSpecificRevision()
    {
        string packageName = "Azure.Storage.Blobs";
        string language = "C#";
        string version = "12.0.0";
        ReviewListItemModel mockReview = CreateMockReview("review123", packageName, language);
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision456", "review123", version);

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(language, packageName, null))
            .ReturnsAsync(mockReview);

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionsAsync(mockReview.Id, version, APIRevisionType.All))
            .ReturnsAsync(new List<APIRevisionListItemModel> { mockRevision });

        RevisionResolveResult result = await _resolver.ResolveByPackageAsync(packageName, language, version);
        Assert.NotNull(result);
        Assert.Equal("revision456", result.RevisionId);
    }

    [Fact]
    public async Task ResolveByPackageAsync_WithVersion_ReturnsNullWhenNotFound()
    {
        string packageName = "Azure.Storage.Blobs";
        string language = "C#";
        string version = "99.0.0"; // Non-existent version
        ReviewListItemModel mockReview = CreateMockReview("review123", packageName, language);

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(language, packageName, null))
            .ReturnsAsync(mockReview);

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionsAsync(mockReview.Id, version, APIRevisionType.All))
            .ReturnsAsync(new List<APIRevisionListItemModel>()); // Empty - version not found

        RevisionResolveResult result = await _resolver.ResolveByPackageAsync(packageName, language, version);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveByPackageAsync_ReviewNotFound_ReturnsNull()
    {
        _mockReviewManager
            .Setup(x => x.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync((ReviewListItemModel)null);

        RevisionResolveResult result = await _resolver.ResolveByPackageAsync("NonExistent", "C#");
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveByPackageAsync_NoRevisions_ReturnsNull()
    {
        ReviewListItemModel mockReview = CreateMockReview("review123", "Azure.Storage.Blobs", "C#");

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync(mockReview);

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(mockReview.Id, null, APIRevisionType.Automatic))
            .ReturnsAsync((APIRevisionListItemModel)null);

        RevisionResolveResult result = await _resolver.ResolveByPackageAsync("Azure.Storage.Blobs", "C#");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "C#")]
    [InlineData("", "C#")]
    public async Task ResolveByPackageAsync_EmptyPackageName_ThrowsArgumentException(string packageName,
        string language)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _resolver.ResolveByPackageAsync(packageName, language));
    }

    [Theory]
    [InlineData("Azure.Storage.Blobs", null)]
    [InlineData("Azure.Storage.Blobs", "")]
    public async Task ResolveByPackageAsync_EmptyLanguage_ThrowsArgumentException(string packageName, string language)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _resolver.ResolveByPackageAsync(packageName, language));
    }

    #endregion

    #region ResolveByLinkAsync Tests

    [Fact]
    public async Task ResolveByLinkAsync_SpaUrlWithReviewIdOnly_ResolvesLatestRevision()
    {
        string link = "https://spa.apiview.dev/review/review123";
        ReviewListItemModel mockReview = CreateMockReview("review123", "Azure.Storage.Blobs", "C#");
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision123", "review123", "1.0.0");

        _mockReviewManager
            .Setup(x => x.GetReviewsAsync(new List<string> { "review123" }, null))
            .ReturnsAsync(new List<ReviewListItemModel> { mockReview });

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(mockReview.Id, null, It.IsAny<APIRevisionType>()))
            .ReturnsAsync(mockRevision);

        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision123", result.RevisionId);
    }

    [Fact]
    public async Task ResolveByLinkAsync_SpaUrlWithRevisionId_ResolvesSpecificRevision()
    {
        string link = "https://spa.apiview.dev/review/review123?activeApiRevisionId=revision456";
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision456", "review123", "12.0.0");

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync("revision456"))
            .ReturnsAsync(mockRevision);

        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision456", result.RevisionId);
    }

    [Fact]
    public async Task ResolveByLinkAsync_LegacyUrl_ParsesCorrectly()
    {
        string link = "https://apiview.dev/Assemblies/Review/review789";
        ReviewListItemModel mockReview = CreateMockReview("review789", "Azure.Core", "C#");
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision789", "review789", "1.0.0");

        _mockReviewManager
            .Setup(x => x.GetReviewsAsync(new List<string> { "review789" }, null))
            .ReturnsAsync(new List<ReviewListItemModel> { mockReview });

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(mockReview.Id, null, APIRevisionType.Automatic))
            .ReturnsAsync(mockRevision);

        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.NotNull(result);
        Assert.Equal("review789", result.ReviewId);
    }

    [Fact]
    public async Task ResolveByLinkAsync_InvalidUrl_ReturnsNull()
    {
        string link = "https://example.com/invalid/path";
        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveByLinkAsync_ReviewNotFound_ReturnsNull()
    {
        string link = "https://spa.apiview.dev/review/nonexistent";
        _mockReviewManager
            .Setup(x => x.GetReviewsAsync(It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<ReviewListItemModel>());

        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveByLinkAsync_RevisionIdInUrl_RevisionNotFound_ReturnsNull()
    {
        string link = "https://spa.apiview.dev/review/review123?activeApiRevisionId=nonexistent";
        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync("nonexistent"))
            .ReturnsAsync((APIRevisionListItemModel)null);

        RevisionResolveResult result = await _resolver.ResolveByLinkAsync(link);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ResolveByLinkAsync_EmptyLink_ThrowsArgumentException(string link)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _resolver.ResolveByLinkAsync(link));
    }

    #endregion

    #region Helper Methods

    private static ReviewListItemModel CreateMockReview(string id, string packageName, string language)
    {
        return new ReviewListItemModel
        {
            Id = id,
            PackageName = packageName,
            Language = language,
            IsApproved = false,
            CreatedBy = "testuser",
            CreatedOn = DateTime.UtcNow.AddDays(-7),
            LastUpdatedOn = DateTime.UtcNow
        };
    }

    private static APIRevisionListItemModel CreateMockRevision(string id, string reviewId, string version)
    {
        return new APIRevisionListItemModel
        {
            Id = id,
            ReviewId = reviewId,
            Language = "C#",
            Files = [new APICodeFileModel { FileId = "file1", PackageVersion = version }],
            ChangeHistory = [],
            Approvers = [],
            AssignedReviewers = [],
            IsApproved = false,
            CreatedBy = "testuser",
            CreatedOn = DateTime.UtcNow.AddDays(-1),
            HeadingsOfSectionsWithDiff = new Dictionary<string, HashSet<int>>()
        };
    }

    #endregion
}
