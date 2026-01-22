using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace APIViewUnitTests;

public class ResolvePackageTests
{
    private readonly Mock<IAPIRevisionsManager> _mockApiRevisionsManager;
    private readonly Mock<ILogger<ResolvePackage>> _mockLogger;
    private readonly Mock<IReviewManager> _mockReviewManager;
    private readonly Mock<ICopilotAuthenticationService> _mockCopilotAuthService;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ResolvePackage package;

    public ResolvePackageTests()
    {
        _mockReviewManager = new Mock<IReviewManager>();
        _mockApiRevisionsManager = new Mock<IAPIRevisionsManager>();
        _mockLogger = new Mock<ILogger<ResolvePackage>>();
        _mockCopilotAuthService = new Mock<ICopilotAuthenticationService>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockConfiguration.Setup(x => x["CopilotServiceEndpoint"]).Returns("https://copilot.test");
        _mockCopilotAuthService.Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test-token");

        package = new ResolvePackage(
            _mockReviewManager.Object,
            _mockApiRevisionsManager.Object,
            _mockCopilotAuthService.Object,
            _mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);
    }

    #region ResolvePackageQuery Tests

    [Fact]
    public async Task ResolvePackageQuery_WithValidPackageAndLanguage_ReturnsResult()
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

        ResolvePackageResponse result = await package.ResolvePackageQuery(packageName, language);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision123", result.RevisionId);
    }

    [Fact]
    public async Task ResolvePackageQuery_WithVersion_FindsSpecificRevision()
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

        ResolvePackageResponse result = await package.ResolvePackageQuery(packageName, language, version);
        Assert.NotNull(result);
        Assert.Equal("revision456", result.RevisionId);
    }

    [Fact]
    public async Task ResolvePackageQuery_WithVersion_ReturnsNullWhenNotFound()
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

        ResolvePackageResponse result = await package.ResolvePackageQuery(packageName, language, version);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePackageQuery_NoRevisions_ReturnsNull()
    {
        ReviewListItemModel mockReview = CreateMockReview("review123", "Azure.Storage.Blobs", "C#");

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync(mockReview);

        _mockApiRevisionsManager
            .Setup(x => x.GetLatestAPIRevisionsAsync(mockReview.Id, null, APIRevisionType.Automatic))
            .ReturnsAsync((APIRevisionListItemModel)null);

        ResolvePackageResponse result = await package.ResolvePackageQuery("Azure.Storage.Blobs", "C#");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "C#")]
    [InlineData("", "C#")]
    public async Task ResolvePackageQuery_EmptyPackageName_ThrowsArgumentException(string packageName,
        string language)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => package.ResolvePackageQuery(packageName, language));
    }

    [Theory]
    [InlineData("Azure.Storage.Blobs", null)]
    [InlineData("Azure.Storage.Blobs", "")]
    public async Task ResolvePackageQuery_EmptyLanguage_ThrowsArgumentException(string packageName, string language)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => package.ResolvePackageQuery(packageName, language));
    }

    [Fact]
    public async Task ResolvePackageQuery_ReviewNotFoundLocally_CopilotReturnsResult_ReturnsResponse()
    {
        string packageQuery = "azure-storage";
        string language = "Python";

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(language, packageQuery, null))
            .ReturnsAsync((ReviewListItemModel)null);

        var copilotResponse = new ResolvePackageResponse
        {
            PackageName = "azure-storage-blob",
            Language = "Python",
            ReviewId = "copilot-review-123",
            Version = "12.0.0",
            RevisionId = "copilot-revision-456",
            RevisionLabel = "Latest"
        };

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(copilotResponse))
        };

        HttpRequestMessage capturedRequest = null;
        string capturedRequestBody = null;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedRequestBody = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(httpResponse);

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        ResolvePackageResponse result = await package.ResolvePackageQuery(packageQuery, language);

        Assert.NotNull(result);
        Assert.Equal("azure-storage-blob", result.PackageName);
        Assert.Equal("copilot-review-123", result.ReviewId);
        Assert.Equal("copilot-revision-456", result.RevisionId);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://copilot.test/api-review/resolve-package", capturedRequest.RequestUri.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);

        Assert.NotNull(capturedRequestBody);
        Assert.Contains("\"packageQuery\":\"azure-storage\"", capturedRequestBody);
        Assert.Contains("\"language\":\"Python\"", capturedRequestBody);
    }

    [Fact]
    public async Task ResolvePackageQuery_ReviewNotFoundLocally_CopilotReturnsNotFound_ReturnsNull()
    {
        string packageQuery = "nonexistent-package";
        string language = "Python";

        _mockReviewManager
            .Setup(x => x.GetReviewAsync(language, packageQuery, null))
            .ReturnsAsync((ReviewListItemModel)null);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

        HttpRequestMessage capturedRequest = null;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        ResolvePackageResponse result = await package.ResolvePackageQuery(packageQuery, language);
        Assert.Null(result);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://copilot.test/api-review/resolve-package", capturedRequest.RequestUri.ToString());
    }

    #endregion

    #region ResolvePackageLink Tests

    [Fact]
    public async Task ResolvePackageLink_SpaUrlWithReviewIdOnly_ResolvesLatestRevision()
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

        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision123", result.RevisionId);
    }

    [Fact]
    public async Task ResolvePackageLink_SpaUrlWithRevisionId_ResolvesSpecificRevision()
    {
        string link = "https://spa.apiview.dev/review/review123?activeApiRevisionId=revision456";
        APIRevisionListItemModel mockRevision = CreateMockRevision("revision456", "review123", "12.0.0");

        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync("revision456"))
            .ReturnsAsync(mockRevision);

        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.NotNull(result);
        Assert.Equal("review123", result.ReviewId);
        Assert.Equal("revision456", result.RevisionId);
    }

    [Fact]
    public async Task ResolvePackageLink_LegacyUrl_ParsesCorrectly()
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

        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.NotNull(result);
        Assert.Equal("review789", result.ReviewId);
    }

    [Fact]
    public async Task ResolvePackageLink_InvalidUrl_ReturnsNull()
    {
        string link = "https://example.com/invalid/path";
        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePackageLink_ReviewNotFound_ReturnsNull()
    {
        string link = "https://spa.apiview.dev/review/nonexistent";
        _mockReviewManager
            .Setup(x => x.GetReviewsAsync(It.IsAny<List<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<ReviewListItemModel>());

        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePackageLink_RevisionIdInUrl_RevisionNotFound_ReturnsNull()
    {
        string link = "https://spa.apiview.dev/review/review123?activeApiRevisionId=nonexistent";
        _mockApiRevisionsManager
            .Setup(x => x.GetAPIRevisionAsync("nonexistent"))
            .ReturnsAsync((APIRevisionListItemModel)null);

        ResolvePackageResponse result = await package.ResolvePackageLink(link);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ResolvePackageLink_EmptyLink_ThrowsArgumentException(string link)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => package.ResolvePackageLink(link));
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
