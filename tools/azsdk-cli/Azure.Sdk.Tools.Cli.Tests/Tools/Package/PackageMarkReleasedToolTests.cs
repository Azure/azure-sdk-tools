using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package;

[TestFixture]
public class PackageMarkReleasedToolTests
{
    private Mock<IApiReviewHubService> apiReviewHubService = null!;
    private Mock<IAPIViewService> apiViewService = null!;
    private PackageMarkReleasedTool tool = null!;

    [SetUp]
    public void Setup()
    {
        apiReviewHubService = new Mock<IApiReviewHubService>();
        apiViewService = new Mock<IAPIViewService>();
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new APIViewMarkReleasedResult
            {
                ReviewId = "review123",
                RevisionId = "revision456",
                IsReleased = false
            });
        tool = new PackageMarkReleasedTool(
            apiReviewHubService.Object,
            apiViewService.Object,
            new TestLogger<PackageMarkReleasedTool>());
    }

    [Test]
    public async Task MarkReleasedAsync_CallsBothBackends()
    {
        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiReviewHub.Succeeded, Is.True);
        Assert.That(response.ApiView.Succeeded, Is.True);
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            "python", "azure-test", "1.0.0", "hash", It.IsAny<CancellationToken>()), Times.Once);
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            "azure-test", "python", "1.0.0",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenReviewHubFails_StillCallsAPIView()
    {
        apiReviewHubService
            .Setup(x => x.MarkPackageReleasedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ARH failed"));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ApiReviewHub.Succeeded, Is.False);
        Assert.That(response.ApiView.Succeeded, Is.True);
        Assert.That(response.ToString(), Does.Contain("API Review Hub: FAILED - ARH failed"));
        Assert.That(response.ToString(), Does.Contain("APIView: SUCCEEDED - Dry run resolved revision revision456 (review review123); revision is not released."));
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenAPIViewFails_PreservesReviewHubSuccess()
    {
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("APIView failed"));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ApiReviewHub.Succeeded, Is.True);
        Assert.That(response.ApiView.Succeeded, Is.False);
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Command_DoesNotExposeEndpointOption()
    {
        var command = tool.GetCommandInstances().Single();

        Assert.That(command.Options.Any(option => option.Name.Contains("endpoint", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public void Command_DoesNotRequireApiHash()
    {
        var command = tool.GetCommandInstances().Single();

        var parseResult = command.Parse("--language python --package-name azure-test --package-version 1.0.0");

        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public void Command_IsNotExposedAsMcpTool()
    {
        var command = tool.GetCommandInstances().Single();

        Assert.That(command, Is.Not.TypeOf<McpCommand>());
    }

    private Task<Azure.Sdk.Tools.Cli.Models.Responses.Package.PackageMarkReleasedResponse> MarkReleasedAsync() =>
        tool.MarkReleasedAsync(
            "python",
            "azure-test",
            "1.0.0",
            "hash");
}