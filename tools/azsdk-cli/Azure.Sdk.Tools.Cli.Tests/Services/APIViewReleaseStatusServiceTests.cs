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

    [TestCase(200, true, true, "approved", "python", "language=Python", "1.0.0", "1.0.0")]
    [TestCase(201, false, true, "packageNameApproved", "python", "language=Python", "4.12.0b3", "4.12.0b3")]
    [TestCase(201, false, true, "packageNameApproved", "csharp", "language=C%23", "4.12.0-beta.3", "4.12.0-beta.3")]
    [TestCase(202, false, false, "packageNamePending", "go", "language=Go", "1.0.0", "1.0.0")]
    public async Task GetApprovalStatusAsync_MapsAPIViewStatusCodes(int statusCode, bool isApproved, bool packageNameApproved, string reason, string language, string expectedLanguageQuery, string packageVersion, string expectedPackageVersion)
    {
        string? capturedEndpoint = null;
        apiViewHttpServiceMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string endpoint, CancellationToken _) =>
            {
                capturedEndpoint = endpoint;
                return (string.Empty, statusCode);
            });

        var result = await service.GetApprovalStatusAsync(language, "Azure.Test", packageVersion, CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(statusCode));
        Assert.That(result.IsApproved, Is.EqualTo(isApproved));
        Assert.That(result.PackageNameApproved, Is.EqualTo(packageNameApproved));
        Assert.That(result.Reason, Is.EqualTo(reason));
        Assert.That(capturedEndpoint, Does.Contain(expectedLanguageQuery));
        Assert.That(capturedEndpoint, Does.Contain("packageName=Azure.Test"));
        Assert.That(capturedEndpoint, Does.Contain($"packageVersion={expectedPackageVersion}"));
    }

    [TestCase("go", "language=Go")]
    [TestCase("rust", "language=Rust")]
    public async Task GetApprovalStatusAsync_MapsSupportedGoAndRustLanguages(string language, string expectedLanguageQuery)
    {
        string? capturedEndpoint = null;
        apiViewHttpServiceMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string endpoint, CancellationToken _) =>
            {
                capturedEndpoint = endpoint;
                return (string.Empty, 202);
            });

        var result = await service.GetApprovalStatusAsync(language, "Azure.Test", "1.0.0", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(202));
        Assert.That(capturedEndpoint, Does.Contain(expectedLanguageQuery));
    }
}