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
            .ReturnsAsync(("""{"reviewId":"review123","revisionId":"revision456","packageName":"azure-test","language":"C#","version":"1.0.0-beta.1","isReleased":false,"releasedOn":null}""", 200));
        service = new APIViewService(httpService.Object, new TestLogger<APIViewService>());
    }

    [Test]
    public async Task MarkPackageReleasedAsync_DefaultsToRelease()
    {
        var result = await service.MarkPackageReleasedAsync("azure test", "C#", "1.0.0-beta.1");

        httpService.Verify(x => x.PostAsync(
            "/api/reviews/mark-released?packageName=azure%20test&language=C%23&version=1.0.0-beta.1&dryRun=false",
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.ReviewId, Is.EqualTo("review123"));
        Assert.That(result.RevisionId, Is.EqualTo("revision456"));
        Assert.That(result.PackageName, Is.EqualTo("azure-test"));
        Assert.That(result.Language, Is.EqualTo("C#"));
        Assert.That(result.Version, Is.EqualTo("1.0.0-beta.1"));
        Assert.That(result.IsReleased, Is.False);
        Assert.That(result.ReleasedOn, Is.Null);
    }
}