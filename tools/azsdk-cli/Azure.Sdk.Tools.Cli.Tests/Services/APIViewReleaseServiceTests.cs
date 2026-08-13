using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class APIViewReleaseServiceTests
{
    private Mock<IAPIViewHttpService> httpService = null!;
    private APIViewService service = null!;

    [SetUp]
    public void Setup()
    {
        httpService = new Mock<IAPIViewHttpService>();
        httpService
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("""{"reviewId":"review123","revisionId":"revision456","isReleased":false,"releasedOn":null}""", 200));
        service = new APIViewService(httpService.Object, new TestLogger<APIViewService>());
    }

    [Test]
    public async Task MarkPackageReleasedAsync_UsesReviewsEndpointWithDryRun()
    {
        var result = await service.MarkPackageReleasedAsync("azure test", "C#", "1.0.0-beta.1");

        httpService.Verify(x => x.PostAsync(
            "/api/reviews/mark-released?packageName=azure%20test&language=C%23&version=1.0.0-beta.1&dryRun=true",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.ReviewId, Is.EqualTo("review123"));
        Assert.That(result.RevisionId, Is.EqualTo("revision456"));
        Assert.That(result.IsReleased, Is.False);
        Assert.That(result.ReleasedOn, Is.Null);
    }
}