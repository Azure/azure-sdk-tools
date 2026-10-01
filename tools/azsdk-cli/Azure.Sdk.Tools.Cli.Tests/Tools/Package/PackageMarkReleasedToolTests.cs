using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Models.APIView;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services.APIView;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Moq;
using System.Net;

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
        apiReviewHubService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new ApiReviewHubMarkReleasedResult
            {
                PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PackageVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PackageName = "azure-test",
                Language = "python",
                Version = "1.0.0",
                ReleasedApiHash = "hash",
                ApprovalStatus = "Approved",
                ApprovalRecordId = "approval-record-id",
                AppliedInheritanceRule = "prereleaseToPrerelease",
                IsReleased = false
            });
        apiViewService = new Mock<IAPIViewService>();
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new APIViewMarkReleasedResult
            {
                ReviewId = "review123",
                RevisionId = "revision456",
                PackageName = "azure-test",
                Language = "python",
                Version = "1.0.0",
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
        Assert.That(response.ApiReviewHubSucceeded, Is.True);
        Assert.That(response.ApiViewSucceeded, Is.True);
        Assert.That(response.ApiReviewHub!.Value.GetProperty("packageVersionId").GetGuid(), Is.EqualTo(Guid.Parse("22222222-2222-2222-2222-222222222222")));
        Assert.That(response.ApiView!.Value.GetProperty("revisionId").GetString(), Is.EqualTo("revision456"));
        Assert.That(response.ToString(), Does.Contain("API Review Hub: SUCCEEDED - Release request resolved package version"));
        Assert.That(response.ToString(), Does.Contain("APIView: SUCCEEDED - Release request resolved revision revision456"));
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            "python", "azure-test", "1.0.0", "hash", "tjprescott", It.IsAny<CancellationToken>(), false), Times.Once);
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            "azure-test", "python", "1.0.0",
            It.IsAny<CancellationToken>(), false), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_JsonContainsRawBackendResponses()
    {
        var response = await MarkReleasedAsync();
        var output = new OutputHelper(OutputHelper.OutputModes.Json).Format(response);

        using var document = System.Text.Json.JsonDocument.Parse(output);
        var reviewHub = document.RootElement.GetProperty("api_review_hub");
        var apiView = document.RootElement.GetProperty("api_view");

        Assert.That(reviewHub.GetProperty("packageVersionId").GetString(), Is.EqualTo("22222222-2222-2222-2222-222222222222"));
        Assert.That(reviewHub.GetProperty("approvalRecordId").GetString(), Is.EqualTo("approval-record-id"));
        Assert.That(reviewHub.GetProperty("appliedInheritanceRule").GetString(), Is.EqualTo("prereleaseToPrerelease"));
        Assert.That(reviewHub.TryGetProperty("succeeded", out _), Is.False);
        Assert.That(apiView.GetProperty("revisionId").GetString(), Is.EqualTo("revision456"));
        Assert.That(apiView.TryGetProperty("message", out _), Is.False);
        Assert.That(document.RootElement.TryGetProperty("api_hash", out _), Is.False);
        Assert.That(document.RootElement.TryGetProperty("language", out _), Is.False);
        Assert.That(document.RootElement.TryGetProperty("package_name", out _), Is.False);
        Assert.That(document.RootElement.TryGetProperty("package_type", out _), Is.False);
        Assert.That(document.RootElement.TryGetProperty("sdk_repo", out _), Is.False);
    }

    [Test]
    public async Task MarkReleasedAsync_JsonOmitsUnavailableReviewHubFields()
    {
        apiReviewHubService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new ApiReviewHubMarkReleasedResult
            {
                PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PackageVersionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PackageName = "azure-test",
                Language = "python",
                Version = "1.0.0",
                ReleasedApiHash = "hash",
                ApprovalStatus = "Approved",
                IsReleased = false
            });

        var response = await MarkReleasedAsync();

        Assert.That(response.ApiReviewHub!.Value.TryGetProperty("approvalRecordId", out _), Is.False);
        Assert.That(response.ApiReviewHub.Value.TryGetProperty("appliedInheritanceRule", out _), Is.False);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenReviewHubFails_StillCallsAPIView()
    {
        apiReviewHubService
            .Setup(x => x.MarkPackageReleasedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("ARH failed"));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiReviewHubSucceeded, Is.False);
        Assert.That(response.ApiReviewHub, Is.Null);
        Assert.That(response.ApiViewSucceeded, Is.True);
        Assert.That(response.ResponseErrors, Is.Empty);
        Assert.That(response.ToString(), Does.Contain("API Review Hub: FAILED - ARH failed"));
        Assert.That(response.ToString(), Does.Contain("APIView: SUCCEEDED - Release request resolved revision revision456 (review review123); revision is not released."));
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), false), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenAPIViewFails_PreservesReviewHubSuccess()
    {
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new HttpRequestException("APIView failed"));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiReviewHubSucceeded, Is.True);
        Assert.That(response.ApiViewSucceeded, Is.False);
        Assert.That(response.ApiView, Is.Null);
        Assert.That(response.ResponseErrors, Is.Empty);
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), false), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenAPIViewReturnsNotFound_DoesNotFailCommand()
    {
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new HttpRequestException("APIView revision not found", null, HttpStatusCode.NotFound));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiViewSucceeded, Is.False);
        Assert.That(response.ApiView, Is.Null);
        Assert.That(response.ResponseErrors, Is.Empty);
        Assert.That(response.ToString(), Does.Contain("APIView: FAILED - APIView revision not found"));
    }

    [Test]
    public async Task MarkReleasedAsync_WhenAPIViewReturnsServerError_ReviewHubSuccessPreventsFailure()
    {
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new HttpRequestException("APIView server error", null, HttpStatusCode.InternalServerError));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiViewSucceeded, Is.False);
        Assert.That(response.ResponseErrors, Is.Empty);
    }

    [Test]
    public async Task MarkReleasedAsync_WhenBothBackendsFail_Fails()
    {
        apiReviewHubService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("ARH failed"));
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new HttpRequestException("APIView failed"));

        var response = await MarkReleasedAsync();

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ApiReviewHubSucceeded, Is.False);
        Assert.That(response.ApiViewSucceeded, Is.False);
        Assert.That(response.ResponseErrors, Is.EquivalentTo(new[]
        {
            "API Review Hub: ARH failed",
            "APIView: APIView failed"
        }));
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
    public async Task MarkReleasedAsync_WithoutApiHash_SkipsReviewHubWithoutFailing()
    {
        var response = await tool.MarkReleasedAsync(
            "python",
            "azure-test",
            "1.0.0",
            string.Empty,
            "tjprescott");
        var output = new OutputHelper(OutputHelper.OutputModes.Json).Format(response);

        using var document = System.Text.Json.JsonDocument.Parse(output);
        var reviewHub = document.RootElement.GetProperty("api_review_hub");

        Assert.That(response.ExitCode, Is.Zero);
        Assert.That(response.ApiReviewHubSucceeded, Is.False);
        Assert.That(response.ApiReviewHubSkipped, Is.True);
        Assert.That(response.ResponseErrors, Is.Empty);
        Assert.That(response.ToString(), Does.Contain("API Review Hub: SKIPPED - Skipped because apiHash is required."));
        Assert.That(reviewHub.GetProperty("skipped").GetBoolean(), Is.True);
        Assert.That(reviewHub.GetProperty("message").GetString(), Is.EqualTo("Skipped because apiHash is required."));
        Assert.That(document.RootElement.GetProperty("operation_status").GetString(), Is.EqualTo("Succeeded"));
        Assert.That(document.RootElement.GetProperty("response_errors").GetArrayLength(), Is.Zero);
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            "azure-test", "python", "1.0.0", It.IsAny<CancellationToken>(), false), Times.Once);
    }

    [Test]
    public async Task MarkReleasedAsync_WithoutApiHash_WhenAPIViewReturnsNotFound_Fails()
    {
        apiViewService
            .Setup(x => x.MarkPackageReleasedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new HttpRequestException("APIView revision not found", null, HttpStatusCode.NotFound));

        var response = await tool.MarkReleasedAsync(
            "python",
            "azure-test",
            "1.0.0",
            string.Empty,
            "tjprescott");

        Assert.That(response.ExitCode, Is.EqualTo(1));
        Assert.That(response.ApiReviewHubSkipped, Is.True);
        Assert.That(response.ApiViewSucceeded, Is.False);
        Assert.That(response.ResponseErrors, Has.One.EqualTo("APIView: APIView revision not found"));
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public void Command_AcceptsRepoOwner()
    {
        var command = tool.GetCommandInstances().Single();

        var parseResult = command.Parse("--language python --package-name azure-test --package-version 1.0.0 --repo-owner tjprescott");

        Assert.That(parseResult.Errors, Is.Empty);
    }

    [Test]
    public async Task Command_DryRun_PassesTrueToBothBackends()
    {
        var command = tool.GetCommandInstances().Single();
        var parseResult = command.Parse("--language python --package-name azure-test --package-version 1.0.0 --api-hash hash --dry-run");

        var response = await tool.HandleCommand(parseResult, CancellationToken.None);

        Assert.That(response.ToString(), Does.Contain("API Review Hub: SUCCEEDED - Dry run resolved package version"));
        Assert.That(response.ToString(), Does.Contain("APIView: SUCCEEDED - Dry run resolved revision revision456"));
        apiReviewHubService.Verify(x => x.MarkPackageReleasedAsync(
            "python", "azure-test", "1.0.0", "hash", string.Empty, It.IsAny<CancellationToken>(), true), Times.Once);
        apiViewService.Verify(x => x.MarkPackageReleasedAsync(
            "azure-test", "python", "1.0.0", It.IsAny<CancellationToken>(), true), Times.Once);
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
            "hash",
            "tjprescott");
}