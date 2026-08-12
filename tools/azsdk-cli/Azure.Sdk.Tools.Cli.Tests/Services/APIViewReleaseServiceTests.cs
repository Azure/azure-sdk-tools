using Azure.Sdk.Tools.Cli.Models.APIView;
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
            .ReturnsAsync(("released", 200));
        httpService
            .Setup(x => x.PostMultipartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("released", 200));
        service = new APIViewService(httpService.Object, new TestLogger<APIViewService>());
    }

    [Test]
    public async Task MarkPackageReleasedAsync_WithReviewToken_UsesCreateEndpoint()
    {
        APIViewReleaseRequest request = CreateRequest();
        request.ReviewTokenFileName = "azure-test_Python.json";
        request.BuildId = "123";
        request.RepoName = "Azure/azure-sdk-for-python";

        await service.MarkPackageReleasedAsync(request);

        httpService.Verify(x => x.PostAsync(
            It.Is<string>(endpoint =>
                endpoint.StartsWith("/autoreview/create?", StringComparison.Ordinal) &&
                endpoint.Contains("setReleaseTag=true", StringComparison.Ordinal) &&
                endpoint.Contains("compareAllRevisions=true", StringComparison.Ordinal) &&
                endpoint.Contains("reviewFilePath=azure-test_Python.json", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
        httpService.Verify(x => x.PostMultipartAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MarkPackageReleasedAsync_WithoutReviewToken_UsesUploadEndpoint()
    {
        APIViewReleaseRequest request = CreateRequest();

        await service.MarkPackageReleasedAsync(request);

        httpService.Verify(x => x.PostMultipartAsync(
            "/autoreview/upload",
            "azure-test.zip",
            It.Is<IReadOnlyDictionary<string, string>>(fields =>
                fields["setReleaseTag"] == "true" &&
                fields["compareAllRevisions"] == "true" &&
                fields["packageVersion"] == "1.0.0"),
            It.IsAny<CancellationToken>()), Times.Once);
        httpService.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static APIViewReleaseRequest CreateRequest() => new()
    {
        SourceFilePath = "azure-test.zip",
        PackageName = "azure-test",
        PackageVersion = "1.0.0",
        PackageType = "client",
        SourceBranch = "main"
    };
}