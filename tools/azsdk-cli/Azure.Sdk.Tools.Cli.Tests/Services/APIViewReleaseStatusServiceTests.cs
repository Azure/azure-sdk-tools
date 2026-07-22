using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class APIViewReleaseStatusServiceTests
{
    private Mock<IAPIViewHttpService> apiViewHttpServiceMock = null!;
    private APIViewReleaseStatusService service = null!;

    [SetUp]
    public void Setup()
    {
        apiViewHttpServiceMock = new Mock<IAPIViewHttpService>();
        service = new APIViewReleaseStatusService(apiViewHttpServiceMock.Object, new TestLogger<APIViewReleaseStatusService>());
    }

    [TestCase(200, true, true, "approved")]
    [TestCase(201, false, true, "packageNameApproved")]
    [TestCase(202, false, false, "packageNamePending")]
    public async Task GetReleaseStatusAsync_MapsAPIViewStatusCodes(int statusCode, bool isApproved, bool packageNameApproved, string reason)
    {
        string? capturedEndpoint = null;
        apiViewHttpServiceMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string endpoint, CancellationToken _) =>
            {
                capturedEndpoint = endpoint;
                return (string.Empty, statusCode);
            });

        var result = await service.GetReleaseStatusAsync("csharp", "Azure.Test", "1.0.0", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(statusCode));
        Assert.That(result.IsApproved, Is.EqualTo(isApproved));
        Assert.That(result.PackageNameApproved, Is.EqualTo(packageNameApproved));
        Assert.That(result.Reason, Is.EqualTo(reason));
        Assert.That(capturedEndpoint, Does.Contain("language=C%23"));
        Assert.That(capturedEndpoint, Does.Contain("packageName=Azure.Test"));
        Assert.That(capturedEndpoint, Does.Contain("packageVersion=1.0.0"));
    }
}