using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using System.Net;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ApiReviewReleaseStatusServiceTests
{
    private Mock<IApiReviewHubService> reviewHubServiceMock = null!;
    private Mock<IAPIViewReleaseStatusService> apiViewServiceMock = null!;
    private ApiReviewReleaseStatusService service = null!;

    [SetUp]
    public void Setup()
    {
        reviewHubServiceMock = new Mock<IApiReviewHubService>();
        apiViewServiceMock = new Mock<IAPIViewReleaseStatusService>();
        service = new ApiReviewReleaseStatusService(reviewHubServiceMock.Object, apiViewServiceMock.Object, new TestLogger<ApiReviewReleaseStatusService>());
    }

    [Test]
    public async Task GetReleaseStatusAsync_UsesReviewHubResult_WhenReviewHubQuerySucceeds()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = true,
                Reason = "approved"
            });

        var result = await service.GetReleaseStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.IsApproved, Is.True);
        Assert.That(result.FinalSource, Is.EqualTo("ApiReviewHub"));
        Assert.That(result.Reason, Is.EqualTo("approved"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView, Is.Null);
        apiViewServiceMock.Verify(x => x.GetReleaseStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetReleaseStatusAsync_QueriesApiView_WhenReviewHubResultIsNotApproved()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiReviewHubReleaseGateResult
            {
                IsApproved = false,
                Reason = "missingApproval"
            });
        apiViewServiceMock
            .Setup(x => x.GetReleaseStatusAsync("python", "pkg", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = true,
                StatusCode = 201,
                Reason = "packageNameApproved",
                Details = ["APIView secondary result"]
            });

        var result = await service.GetReleaseStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("ApiReviewHub"));
        Assert.That(result.Reason, Is.EqualTo("missingApproval"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(200));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(201));
        Assert.That(result.ApiView?.PackageNameApproved, Is.True);
    }

    [Test]
    public async Task GetReleaseStatusAsync_FallsBackToApiView_WhenReviewHubQueryFails()
    {
        reviewHubServiceMock
            .Setup(x => x.GetReleaseGateStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("hub failed", null, HttpStatusCode.InternalServerError));
        apiViewServiceMock
            .Setup(x => x.GetReleaseStatusAsync("python", "pkg", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiViewReleaseStatusResult
            {
                IsApproved = false,
                PackageNameApproved = true,
                StatusCode = 201,
                Reason = "packageNameApproved",
                Details = ["APIView fallback result"]
            });

        var result = await service.GetReleaseStatusAsync("https://endpoint", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.FinalSource, Is.EqualTo("APIView"));
        Assert.That(result.Reason, Is.EqualTo("packageNameApproved"));
        Assert.That(result.ReviewHub.StatusCode, Is.EqualTo(500));
        Assert.That(result.ReviewHub.Error, Does.Contain("hub failed"));
        Assert.That(result.ApiView?.StatusCode, Is.EqualTo(201));
        Assert.That(result.ApiView?.PackageNameApproved, Is.True);
    }
}