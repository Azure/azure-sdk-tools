using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using System.Net;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class PackageReleaseStatusServiceTests
{
    private Mock<IApiReviewHubService> reviewHubServiceMock = null!;
    private Mock<IAPIViewReleaseStatusService> apiViewServiceMock = null!;
    private PackageReleaseStatusService service = null!;

    [SetUp]
    public void Setup()
    {
        reviewHubServiceMock = new Mock<IApiReviewHubService>();
        apiViewServiceMock = new Mock<IAPIViewReleaseStatusService>();
        service = new PackageReleaseStatusService(reviewHubServiceMock.Object, apiViewServiceMock.Object, new TestLogger<PackageReleaseStatusService>());
    }

    [Test]
    public async Task GetApprovalStatusAsync_UsesReviewHubResult_WhenReviewHubQuerySucceeds()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = true,
                Reason = "approved"
            });

        var result = await service.GetApprovalStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.True);
        Assert.That(result.FinalSource, Is.EqualTo("ApiReviewHub"));
        Assert.That(result.Reason, Is.EqualTo("approved"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView, Is.Null);
        apiViewServiceMock.Verify(x => x.GetApprovalStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetApprovalStatusAsync_QueriesApiView_WhenReviewHubResultIsNotApproved()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = false,
                Reason = "missingApproval"
            });
        apiViewServiceMock
            .Setup(x => x.GetApprovalStatusAsync("python", "pkg", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = true,
                StatusCode = 201,
                Reason = "packageNameApproved",
                Details = ["APIView secondary result"]
            });

        var result = await service.GetApprovalStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("ApiReviewHub"));
        Assert.That(result.Reason, Is.EqualTo("missingApproval"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(201));
        Assert.That(result.ApiView?.PackageNameApproved, Is.True);
    }

    [Test]
    public async Task GetApprovalStatusAsync_FallsBackToApiView_WhenReviewHubQueryFails()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("hub failed", null, HttpStatusCode.InternalServerError));
        apiViewServiceMock
            .Setup(x => x.GetApprovalStatusAsync("python", "pkg", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = true,
                StatusCode = 201,
                Reason = "packageNameApproved",
                Details = ["APIView fallback result"]
            });

        var result = await service.GetApprovalStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("APIView"));
        Assert.That(result.Reason, Is.EqualTo("packageNameApproved"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(500));
        Assert.That(result.ReviewHub.Error, Does.Contain("hub failed"));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(201));
        Assert.That(result.ApiView?.PackageNameApproved, Is.True);
    }

    [Test]
    public async Task GetApprovalStatusAsync_UsesApiViewResult_WhenReviewHubRepositoryIsNotSupported()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = false,
                Reason = "repositoryNotSupported"
            });
        apiViewServiceMock
            .Setup(x => x.GetApprovalStatusAsync("python", "pkg", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = false,
                StatusCode = 200,
                Reason = "packageNamePending",
                Details = ["APIView fallback result"]
            });

        var result = await service.GetApprovalStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("APIView"));
        Assert.That(result.Reason, Is.EqualTo("packageNamePending"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task GetApprovalStatusAsync_ReturnsReviewNotFound_WhenReviewHubRepositoryIsNotSupported_AndApiViewReturns404()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "java", "azure-keyvault-keys", "4.12.0b3", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = false,
                Reason = "repositoryNotSupported"
            });
        apiViewServiceMock
            .Setup(x => x.GetApprovalStatusAsync("java", "azure-keyvault-keys", "4.12.0b3", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("review not found", null, HttpStatusCode.NotFound));

        var result = await service.GetApprovalStatusAsync("https://endpoint", "java", "azure-keyvault-keys", "4.12.0b3", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("APIView"));
        Assert.That(result.Reason, Is.EqualTo("reviewNotFound"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(404));
        Assert.That(result.ApiView?.Reason, Is.EqualTo("reviewNotFound"));
    }

    [Test]
    public async Task GetApprovalStatusAsync_SkipsApiViewFallback_WhenLanguageIsNotSupportedByApiView()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "cpp", "pkg", "1.0.0", "hash", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = false,
                Reason = "repositoryNotSupported"
            });

        var result = await service.GetApprovalStatusAsync("https://endpoint", "cpp", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("None"));
        Assert.That(result.Reason, Is.EqualTo("apiViewLanguageNotSupported"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView?.Reason, Is.EqualTo("languageNotSupported"));
        Assert.That(result.ApiView?.Details, Is.EqualTo(new[] { "APIView does not support cpp for release gating." }));
        apiViewServiceMock.Verify(x => x.GetApprovalStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}