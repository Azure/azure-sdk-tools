// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Tests confirmed spec-target persistence through the public release-plan and generation tools.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
internal class ReleasePlanSpecHelperTests
{
    private const string PinnedCommit = "0123456789abcdef0123456789abcdef01234567";
    private const string NewCommit = "fedcba9876543210fedcba9876543210fedcba98";
    private const string PreviewApiVersion = "2021-10-01-preview";
    private const string StableApiVersion = "2021-11-01";
    private const string RelativeProjectPath = "specification/testcontoso/Contoso.Management";
    private const string SpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/123";
    private const string PackageName = "azure-resourcemanager-contoso";
    private const int WorkItemId = 456;

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConfirmedPreviewTarget_RegeneratesAtStoredCommitAfterSpecMetadataMovesToGa(bool initiallyMerged)
    {
        using var directory = TempDirectory.Create("release-plan-persistence");
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var projectPath = CopyTypeSpecFixture(directory.DirectoryPath);
        var metadata = CreateMetadata(projectPath, PreviewApiVersion, [PreviewApiVersion, StableApiVersion]);
        var git = new Mock<IGitHelper>(MockBehavior.Strict);
        var process = new Mock<IProcessHelper>(MockBehavior.Strict);
        var npx = new Mock<INpxHelper>(MockBehavior.Strict);
        var localPaths = new TypeSpecHelper(git.Object, process.Object);
        var typeSpec = new Mock<ITypeSpecHelper>(MockBehavior.Strict);
        typeSpec.Setup(helper => helper.IsUrl(projectPath)).Returns(false);
        typeSpec.Setup(helper => helper.IsValidTypeSpecProjectPath(projectPath))
            .Returns(() => localPaths.IsValidTypeSpecProjectPath(projectPath));
        typeSpec.Setup(helper => helper.IsTypeSpecProjectForMgmtPlane(projectPath))
            .Returns(() => localPaths.IsTypeSpecProjectForMgmtPlane(projectPath));
        typeSpec.Setup(helper => helper.GetSpecRepoRootPath(projectPath)).Returns(directory.DirectoryPath);
        typeSpec.Setup(helper => helper.GetTypeSpecProjectRelativePath(projectPath)).Returns(RelativeProjectPath);
        typeSpec.Setup(helper => helper.IsRepoPathForSpecRepoAsync(directory.DirectoryPath, ct)).ReturnsAsync(true);
        typeSpec.Setup(helper => helper.IsRepoPathForPublicSpecRepoAsync(directory.DirectoryPath, ct)).ReturnsAsync(true);
        typeSpec.Setup(helper => helper.ParseTypeSpecProjectAsync(projectPath, npx.Object, It.IsAny<ILogger>(), ct))
            .ReturnsAsync(() => metadata);
        typeSpec.Setup(helper => helper.ValidateReleasePlanSnapshotAsync(
                projectPath, It.IsAny<string>(), npx.Object, It.IsAny<ILogger>(), ct))
            .ReturnsAsync(() => metadata);

        var pullRequests = new MockGitHubService
        {
            ConfiguredPullRequestMerged = initiallyMerged,
            ConfiguredHeadSha = initiallyMerged ? NewCommit : PinnedCommit,
            ConfiguredMergeCommitSha = PinnedCommit
        };
        var github = new Mock<IGitHubService>(MockBehavior.Strict);
        github.Setup(service => service.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, ct))
            .Returns(() => pullRequests.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, ct));

        // Persist the actual create/update inputs instead of supplying a preconstructed pinned plan.
        ReleasePlanWorkItem? storedPlan = null;
        var storage = new Mock<IDevOpsService>(MockBehavior.Strict);
        storage.Setup(service => service.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(
                RelativeProjectPath, PreviewApiVersion, ApiReleaseType.PublicPreview, ct))
            .ReturnsAsync((ReleasePlanWorkItem?)null);
        storage.Setup(service => service.GetReleasePlanAsync(SpecPullRequestUrl, ApiReleaseType.PublicPreview, ct))
            .ReturnsAsync(new ReleasePlanWorkItem());
        storage.Setup(service => service.CreateReleasePlanWorkItemAsync(It.IsAny<ReleasePlanWorkItem>(), ct))
            .Callback<ReleasePlanWorkItem, CancellationToken>((plan, _) => storedPlan = plan)
            .ReturnsAsync(new WorkItem
            {
                Id = WorkItemId,
                Fields = new Dictionary<string, object> { ["Custom.ReleasePlanID"] = 1234 }
            });
        storage.Setup(service => service.UpdateReleasePlanSDKDetailsAsync(WorkItemId, It.IsAny<List<SDKInfo>>(), ct))
            .Callback<int, List<SDKInfo>, CancellationToken>((_, packages, _) => storedPlan!.SDKInfo = packages)
            .ReturnsAsync(true);
        storage.Setup(service => service.GetReleasePlanForWorkItemAsync(WorkItemId, ct)).ReturnsAsync(() => storedPlan!);
        storage.Setup(service => service.ResolveReleasePlanByIdAsync(WorkItemId, ct)).ReturnsAsync(() => storedPlan);
        storage.Setup(service => service.GetActiveReleasePlansByTypeSpecProjectPathAsync(RelativeProjectPath, ApiReleaseType.Unknown, ct))
            .ReturnsAsync(new List<ReleasePlanWorkItem>());
        storage.Setup(service => service.RunSDKGenerationPipelineAsync(
                PinnedCommit, RelativeProjectPath, PreviewApiVersion, "beta", "Java", WorkItemId, "", false, ct))
            .ReturnsAsync(new Build { Id = 100, Status = BuildStatus.Completed });

        var user = new Mock<IUserHelper>(MockBehavior.Strict);
        user.Setup(helper => helper.GetUserEmail(ct)).ReturnsAsync("snapshot-test@example.com");
        var environment = new Mock<IEnvironmentHelper>(MockBehavior.Strict);
        environment.Setup(helper => helper.GetBooleanVariable("AZSDKTOOLS_AGENT_TESTING", false)).Returns(false);
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero));
        clock.SetupGet(provider => provider.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        using var httpClient = new HttpClient(Mock.Of<HttpMessageHandler>());
        var releasePlans = new ReleasePlanTool(
            storage.Object, git.Object, typeSpec.Object, NullLogger<ReleasePlanTool>.Instance,
            user.Object, github.Object, environment.Object, new InputSanitizer(), httpClient,
            npx.Object, Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>(), clock.Object);
        var generation = new SpecWorkflowTool(
            github.Object, storage.Object, typeSpec.Object, NullLogger<SpecWorkflowTool>.Instance, new InputSanitizer());

        var preview = await releasePlans.CreateReleasePlan(
            null, projectPath, "October 2026", "Public Preview", specPullRequestUrl: SpecPullRequestUrl,
            isTestReleasePlan: true, apiVersion: PreviewApiVersion, ct: ct);

        Assert.Multiple(() =>
        {
            Assert.That(preview.ResponseError, Is.Null);
            Assert.That(preview.RequiresConfirmation, Is.True);
            Assert.That(preview.ReleasePlanDetails, Is.Null);
            Assert.That(preview.ProposedSpecTarget?.SpecCommitSHA, Is.EqualTo(PinnedCommit));
            Assert.That(preview.ProposedSpecTarget?.ApiVersion, Is.EqualTo(PreviewApiVersion));
            Assert.That(preview.ProposedSpecTarget?.IsSpecMerged, Is.EqualTo(initiallyMerged));
            Assert.That(storedPlan, Is.Null);
        });
        storage.VerifyNoOtherCalls();

        var confirmed = await releasePlans.CreateReleasePlan(
            null, projectPath, "October 2026", "Public Preview", specPullRequestUrl: SpecPullRequestUrl,
            serviceTreeId: "87654321-4321-8765-1234-210987654321", productTreeId: "12345678-1234-5678-9012-123456789012",
            isTestReleasePlan: true, apiVersion: PreviewApiVersion, specCommitSha: PinnedCommit, confirmTarget: true, ct: ct);

        Assert.That(confirmed.ResponseError, Is.Null);
        Assert.That(storedPlan, Is.Not.Null);
        Assert.That(confirmed.ReleasePlanDetails, Is.SameAs(storedPlan));
        Assert.Multiple(() =>
        {
            Assert.That(storedPlan!.SpecCommitSHA, Is.EqualTo(PinnedCommit));
            Assert.That(storedPlan.SpecAPIVersion, Is.EqualTo(PreviewApiVersion));
            Assert.That(storedPlan.SDKReleaseType, Is.EqualTo("beta"));
            Assert.That(storedPlan.APISpecProjectPath, Is.EqualTo(RelativeProjectPath));
            Assert.That(storedPlan.ActiveSpecPullRequest, Is.EqualTo(SpecPullRequestUrl));
            Assert.That(storedPlan.SDKInfo.Select(package => package.PackageName), Is.EqualTo(new[] { PackageName }));
        });

        var firstGeneration = await generation.RunGenerateSdkAsync(
            projectPath, "beta", "Java", workItemId: WorkItemId, specCommitSha: PinnedCommit, ct: ct);
        Assert.That(firstGeneration.Status, Is.EqualTo("Success"));
        Assert.That(firstGeneration.ResponseErrors, Is.Empty);
        storage.Verify(service => service.RunSDKGenerationPipelineAsync(
            PinnedCommit, RelativeProjectPath, PreviewApiVersion, "beta", "Java", WorkItemId, "", false, ct), Times.Once);

        // Only external spec state advances. Do not rewrite the fixture or the saved release plan.
        metadata = CreateMetadata(projectPath, StableApiVersion, [StableApiVersion]);
        pullRequests.ConfiguredPullRequestMerged = true;
        pullRequests.ConfiguredHeadSha = NewCommit;
        pullRequests.ConfiguredMergeCommitSha = NewCommit;

        var regeneration = await generation.RunGenerateSdkAsync(
            projectPath, "beta", "Java", workItemId: WorkItemId, specCommitSha: PinnedCommit, ct: ct);

        Assert.Multiple(() =>
        {
            Assert.That(regeneration.Status, Is.EqualTo("Success"));
            Assert.That(regeneration.ResponseErrors, Is.Empty);
            Assert.That(regeneration.Details, Has.Some.Contains(PinnedCommit).And.Contains(PreviewApiVersion));
            Assert.That(storedPlan!.SpecCommitSHA, Is.EqualTo(PinnedCommit));
            Assert.That(storedPlan.SpecAPIVersion, Is.EqualTo(PreviewApiVersion));
            Assert.That(storedPlan.SDKReleaseType, Is.EqualTo("beta"));
        });
        storage.Verify(service => service.RunSDKGenerationPipelineAsync(
            PinnedCommit, RelativeProjectPath, PreviewApiVersion, "beta", "Java", WorkItemId, "", false, ct), Times.Exactly(2));
        storage.Verify(service => service.RunSDKGenerationPipelineAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        storage.Verify(service => service.CreateReleasePlanWorkItemAsync(It.IsAny<ReleasePlanWorkItem>(), ct), Times.Once);
        storage.Verify(service => service.UpdateReleasePlanSDKDetailsAsync(WorkItemId, It.IsAny<List<SDKInfo>>(), ct), Times.Once);
        storage.Verify(service => service.UpdateSpecPullRequestAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(service => service.UpdateConfirmedReleaseTargetAsync(
            It.IsAny<int>(), It.IsAny<ReleasePlanSpecTarget>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(service => service.UpdateApiSpecVersionAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        typeSpec.Verify(helper => helper.ValidateReleasePlanSnapshotAsync(
            projectPath, PinnedCommit, npx.Object, It.IsAny<ILogger>(), ct), Times.Exactly(2));
        typeSpec.Verify(helper => helper.ValidateReleasePlanSnapshotAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        typeSpec.Verify(helper => helper.ParseTypeSpecProjectAsync(
            It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Never);
        github.Verify(service => service.GetPullRequestAsync("Azure", "azure-rest-api-specs", 123, ct), Times.Exactly(2));
        github.VerifyNoOtherCalls();
        git.VerifyNoOtherCalls();
        process.VerifyNoOtherCalls();
        npx.VerifyNoOtherCalls();
    }

    private static string CopyTypeSpecFixture(string root)
    {
        var projectPath = Path.Combine(root, "specification", "testcontoso", "Contoso.Management");
        var source = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "TypeSpecTestData", "specification", "testcontoso", "Contoso.Management");
        Directory.CreateDirectory(projectPath);
        foreach (var file in new[] { "tspconfig.yaml", "main.tsp", "employee.tsp" })
        {
            File.Copy(Path.Combine(source, file), Path.Combine(projectPath, file));
        }
        return projectPath;
    }

    private static TypeSpecProject CreateMetadata(string projectPath, string apiVersion, List<string> availableVersions)
    {
        var project = TypeSpecProject.ParseTypeSpecConfig(projectPath);
        project.Packages =
        [
            new PackageInfo
            {
                Language = SdkLanguage.Java,
                PackageName = PackageName,
                ApiVersion = apiVersion,
                TypeSpecSdkType = "management"
            }
        ];
        project.AvailableApiVersions = availableVersions;
        return project;
    }
}