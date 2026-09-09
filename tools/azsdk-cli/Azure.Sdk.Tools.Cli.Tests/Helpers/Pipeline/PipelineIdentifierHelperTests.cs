// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Pipeline;

/// <summary>
/// Seam: <see cref="PipelineIdentifierHelper"/> turns a user-supplied identifier (build id, DevOps URL,
/// GitHub PR URL, or bare PR number) into resolved builds and the GitHub commit they ran against, given
/// <see cref="IDevOpsService"/>, <see cref="IGitHubService"/>, and <see cref="IGitHelper"/>.
/// </summary>
[TestFixture]
public class PipelineIdentifierHelperTests
{
    // Shapes taken from a real azure-sdk-for-net pull request and its Azure Pipelines checks.
    private const int BuildId = 5209385;
    private const string PublicBuildUrl = "https://dev.azure.com/azure-sdk/public/_build/results?buildId=5209385&view=results";
    private const string PrUrl = "https://github.com/Azure/azure-sdk-for-net/pull/44941";
    private const string ProjectGuid = "590cfd2a-581c-4dcb-a12e-6568ce786175";

    private Mock<IDevOpsService> devOpsService;
    private Mock<IGitHubService> gitHubService;
    private Mock<IGitHelper> gitHelper;
    private PipelineIdentifierHelper helper;

    [SetUp]
    public void SetUp()
    {
        devOpsService = new Mock<IDevOpsService>(MockBehavior.Loose);
        gitHubService = new Mock<IGitHubService>(MockBehavior.Loose);
        gitHelper = new Mock<IGitHelper>(MockBehavior.Loose);
        helper = new PipelineIdentifierHelper(
            devOpsService.Object,
            gitHubService.Object,
            gitHelper.Object,
            new TestLogger<PipelineIdentifierHelper>());
    }

    #region Parse

    [Test]
    public void Parse_NumericIdentifier_ReturnsBuildIdWithNoProject()
    {
        var (buildId, project) = helper.Parse("5209385");

        Assert.Multiple(() =>
        {
            Assert.That(buildId, Is.EqualTo(BuildId));
            Assert.That(project, Is.Null);
        });
    }

    [Test]
    public void Parse_PipelineUrl_ReturnsBuildIdAndProjectFromPath()
    {
        var (buildId, project) = helper.Parse(PublicBuildUrl);

        Assert.Multiple(() =>
        {
            Assert.That(buildId, Is.EqualTo(BuildId));
            Assert.That(project, Is.EqualTo("public"));
        });
    }

    [Test]
    public void Parse_UrlWithoutProjectSegment_ReturnsNullProject()
    {
        var (buildId, project) = helper.Parse("https://dev.azure.com/azure-sdk/?buildId=5209385");

        Assert.Multiple(() =>
        {
            Assert.That(buildId, Is.EqualTo(BuildId));
            Assert.That(project, Is.Null);
        });
    }

    [Test]
    public void Parse_IdentifierThatIsNeitherNumberNorUri_ThrowsArgumentException()
    {
        Assert.That(() => helper.Parse("net - core - ci"), Throws.ArgumentException);
    }

    [Test]
    public void Parse_UrlWithoutBuildIdQuery_ThrowsArgumentException()
    {
        Assert.That(
            () => helper.Parse("https://dev.azure.com/azure-sdk/public/_build/results?view=logs"),
            Throws.ArgumentException);
    }

    [Test]
    public void Parse_UrlWithNonNumericBuildId_ThrowsArgumentException()
    {
        Assert.That(
            () => helper.Parse("https://dev.azure.com/azure-sdk/public/_build/results?buildId=latest"),
            Throws.ArgumentException);
    }

    #endregion

    #region GetPipelineUrl

    [TestCase("azure-sdk-public", "public")]
    [TestCase("Azure-SDK-Internal", "internal")]
    public void GetPipelineUrl_LegacyProjectName_NormalizesToShortName(string project, string expected)
    {
        var url = helper.GetPipelineUrl(project, BuildId);

        Assert.That(url, Is.EqualTo($"https://dev.azure.com/azure-sdk/{expected}/_build/results?buildId=5209385"));
    }

    [Test]
    public void GetPipelineUrl_UnrecognizedProjectName_UsesItVerbatim()
    {
        var url = helper.GetPipelineUrl("azure-sdk-for-net", BuildId);

        Assert.That(url, Is.EqualTo("https://dev.azure.com/azure-sdk/azure-sdk-for-net/_build/results?buildId=5209385"));
    }

    #endregion

    #region TryParseGitHubPrLink

    [Test]
    public void TryParseGitHubPrLink_PullRequestUrl_ReturnsOwnerRepoAndNumber()
    {
        var link = helper.TryParseGitHubPrLink(PrUrl);

        Assert.That(link, Is.EqualTo(new GitHubPrLink("Azure", "azure-sdk-for-net", 44941)));
    }

    [TestCase("https://github.com/Azure/azure-sdk-for-net/issues/44941", Description = "an issue, not a pull request")]
    public void TryParseGitHubPrLink_NonPullRequestUrl_ReturnsNull(string identifier)
    {
        Assert.That(helper.TryParseGitHubPrLink(identifier), Is.Null);
    }

    #endregion

    #region TryResolveGitHubPrAsync

    [Test]
    public async Task TryResolveGitHubPrAsync_PullRequestUrl_ResolvesWithoutConsultingGit()
    {
        var link = await helper.TryResolveGitHubPrAsync(PrUrl, CancellationToken.None);

        Assert.That(link, Is.EqualTo(new GitHubPrLink("Azure", "azure-sdk-for-net", 44941)));
        gitHelper.Verify(
            g => g.GetRepoFullNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase("44941")]
    [TestCase("999999", Description = "the largest number still treated as a PR number")]
    public async Task TryResolveGitHubPrAsync_BarePrNumber_ResolvesRepoFromWorkingDirectory(string identifier)
    {
        GivenCurrentRepo("Azure/azure-sdk-for-net");

        var link = await helper.TryResolveGitHubPrAsync(identifier, CancellationToken.None);

        Assert.That(link, Is.EqualTo(new GitHubPrLink("Azure", "azure-sdk-for-net", int.Parse(identifier))));
    }

    [Test]
    public async Task TryResolveGitHubPrAsync_NumberTooLargeForAPrNumber_ReturnsNull()
    {
        GivenCurrentRepo("Azure/azure-sdk-for-net");

        var link = await helper.TryResolveGitHubPrAsync("1000000", CancellationToken.None);

        Assert.That(link, Is.Null);
    }

    [Test]
    public async Task TryResolveGitHubPrAsync_WorkingDirectoryIsNotARepo_ReturnsNull()
    {
        gitHelper
            .Setup(g => g.GetRepoFullNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("not a git repository"));

        var link = await helper.TryResolveGitHubPrAsync("44941", CancellationToken.None);

        Assert.That(link, Is.Null);
    }

    [Test]
    public async Task TryResolveGitHubPrAsync_RepoNameIsNotOwnerSlashRepo_ReturnsNull()
    {
        GivenCurrentRepo("azure-sdk-for-net");

        var link = await helper.TryResolveGitHubPrAsync("44941", CancellationToken.None);

        Assert.That(link, Is.Null);
    }

    #endregion

    #region GetPipelineProjectAsync

    [Test]
    public async Task GetPipelineProjectAsync_BuildWithProject_ReturnsProjectName()
    {
        GivenBuildDetails(BuildFor("public"));

        var project = await helper.GetPipelineProjectAsync(BuildId, null, CancellationToken.None);

        Assert.That(project, Is.EqualTo("public"));
    }

    [Test]
    public void GetPipelineProjectAsync_BuildWithoutProject_Throws()
    {
        GivenBuildDetails(new Build());

        Assert.That(
            async () => await helper.GetPipelineProjectAsync(BuildId, null, CancellationToken.None),
            Throws.Exception.With.Message.Contains("Failed to parse project name"));
    }

    #endregion

    #region ResolveBuildsAsync - DevOps identifiers

    [Test]
    public async Task ResolveBuildsAsync_BuildId_ReturnsBuildWithProjectStatusAndResult()
    {
        GivenBuildDetails(BuildFor("public", BuildStatus.Completed, BuildResult.PartiallySucceeded));

        var build = (await helper.ResolveBuildsAsync("5209385", ct: CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(build.BuildId, Is.EqualTo(BuildId));
            Assert.That(build.Project, Is.EqualTo("public"));
            Assert.That(build.Status, Is.EqualTo("completed"));
            Assert.That(build.Result, Is.EqualTo("partially_succeeded"));
            Assert.That(build.PipelineUrl, Is.EqualTo("https://dev.azure.com/azure-sdk/public/_build/results?buildId=5209385"));
        });
    }

    [Test]
    public async Task ResolveBuildsAsync_BuildStillRunning_MarksOnlyTheResultUnavailable()
    {
        // A run that has not finished has no result yet, which is not the same as a build whose details
        // could not be read at all.
        GivenBuildDetails(BuildFor("public", BuildStatus.InProgress, result: null));

        var build = (await helper.ResolveBuildsAsync("5209385", ct: CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(build.Status, Is.EqualTo("in_progress"));
            Assert.That(build.Result, Is.EqualTo(AzurePipelineBuild.StatusUnavailable));
        });
    }

    [Test]
    public async Task ResolveBuildsAsync_BuildDetailsCannotBeRead_MarksStatusUnavailable()
    {
        devOpsService
            .Setup(d => d.GetBuildDetailsAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("no access to the internal project"));

        var build = (await helper.ResolveBuildsAsync("5209385", ct: CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(build.Status, Is.EqualTo(AzurePipelineBuild.StatusUnavailable));
            Assert.That(build.Result, Is.EqualTo(AzurePipelineBuild.StatusUnavailable));
        });
    }

    [Test]
    public async Task ResolveBuildsAsync_ExplicitProject_IsUsedWhenTheIdentifierCarriesNone()
    {
        // The build details deliberately carry no project, so the resolved project can only have come
        // from the caller-supplied argument.
        GivenBuildDetails(new Build { Status = BuildStatus.Completed, Result = BuildResult.Failed });

        var build = (await helper.ResolveBuildsAsync("5209385", project: "internal", ct: CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(build.Project, Is.EqualTo("internal"));
            Assert.That(build.PipelineUrl, Is.EqualTo("https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5209385"));
        });
    }

    [Test]
    public void ResolveBuildsAsync_UnrecognizedProjectName_ThrowsArgumentException()
    {
        Assert.That(
            async () => await helper.ResolveBuildsAsync(
                "https://dev.azure.com/azure-sdk/azure-sdk-for-net/_build/results?buildId=5209385",
                ct: CancellationToken.None),
            Throws.ArgumentException);
    }

    [Test]
    public async Task ResolveBuildsAsync_ProjectGuid_ResolvesItToTheProjectName()
    {
        GivenBuildDetails(BuildFor("internal", BuildStatus.Completed, BuildResult.Failed));

        var build = (await helper.ResolveBuildsAsync(
            $"https://dev.azure.com/azure-sdk/{ProjectGuid}/_build/results?buildId=5209385",
            ct: CancellationToken.None)).Single();

        Assert.That(build.Project, Is.EqualTo("internal"));
    }

    [Test]
    public void ResolveBuildsAsync_ProjectGuidThatCannotBeResolved_ThrowsArgumentException()
    {
        devOpsService
            .Setup(d => d.GetBuildDetailsAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        Assert.That(
            async () => await helper.ResolveBuildsAsync(
                $"https://dev.azure.com/azure-sdk/{ProjectGuid}/_build/results?buildId=5209385",
                ct: CancellationToken.None),
            Throws.ArgumentException);
    }

    #endregion

    #region ResolveBuildsAsync - GitHub pull requests

    [Test]
    public async Task ResolveBuildsAsync_PullRequest_ReturnsOnlyFailedAzurePipelinesChecks()
    {
        GivenBuildDetails(BuildFor("public", BuildStatus.Completed, BuildResult.Failed));
        GivenPrChecks(
            AzurePipelinesCheck("net - core - ci", "FAILURE", DetailsUrlFor(5209385)),
            AzurePipelinesCheck("net - template - ci", "SUCCESS", DetailsUrlFor(5209386)),
            new PrCheckRun { Name = "analyze", Conclusion = "FAILURE", AppName = "GitHub Actions", DetailsUrl = "https://github.com/Azure/azure-sdk-for-net/actions/runs/12345678" });

        var builds = await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        Assert.That(builds.Select(b => b.BuildId), Is.EqualTo(new[] { 5209385 }));
    }

    [Test]
    public async Task ResolveBuildsAsync_PullRequestWithNonFailureFailedConclusion_StillResolvesTheBuild()
    {
        // A cancelled or timed-out Azure Pipelines check is still a failure worth analyzing, so it must
        // resolve to a build even though its conclusion is not the literal "FAILURE".
        GivenBuildDetails(BuildFor("public", BuildStatus.Completed, BuildResult.Failed));
        GivenPrChecks(
            AzurePipelinesCheck("net - core - ci", "TIMED_OUT", DetailsUrlFor(5209385)),
            AzurePipelinesCheck("net - template - ci", "SUCCESS", DetailsUrlFor(5209386)));

        var builds = await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        Assert.That(builds.Select(b => b.BuildId), Is.EqualTo(new[] { 5209385 }));
    }

    [Test]
    public async Task ResolveBuildsAsync_PullRequestWithSeveralChecksOnOneBuild_ReturnsThatBuildOnce()
    {
        GivenBuildDetails(BuildFor("public", BuildStatus.Completed, BuildResult.Failed));
        GivenPrChecks(
            AzurePipelinesCheck("net - core - ci", "FAILURE", DetailsUrlFor(5209385) + "&view=logs"),
            AzurePipelinesCheck("net - core - ci (Analyze)", "FAILURE", DetailsUrlFor(5209385) + "&view=results"));

        var builds = await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        Assert.That(builds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ResolveBuildsAsync_FailedCheckWithoutDetailsUrl_IsSkipped()
    {
        GivenPrChecks(AzurePipelinesCheck("net - core - ci", "FAILURE", detailsUrl: null));

        var builds = await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        Assert.That(builds, Is.Empty);
    }

    [Test]
    public async Task ResolveBuildsAsync_FailedCheckWithNonDevOpsDetailsUrl_IsSkipped()
    {
        GivenPrChecks(AzurePipelinesCheck("net - core - ci", "FAILURE", "https://aka.ms/azsdk/checkenforcer"));

        var builds = await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        Assert.That(builds, Is.Empty);
    }

    [Test]
    public async Task ResolveBuildsAsync_CheckUrlWithoutAProject_KeepsTheCheckUrlAsThePipelineUrl()
    {
        // Nothing in this URL identifies a project, so no canonical pipeline URL can be built for it
        // and the check's own link is the only thing left to point the caller at.
        const string DetailsUrl = "https://dev.azure.com/azure-sdk/?buildId=5209385";
        GivenBuildDetails(new Build { Status = BuildStatus.Completed, Result = BuildResult.Failed });
        GivenPrChecks(AzurePipelinesCheck("net - core - ci", "FAILURE", DetailsUrl));

        var build = (await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None)).Single();

        Assert.That(build.PipelineUrl, Is.EqualTo(DetailsUrl));
    }

    [Test]
    public async Task ResolveBuildsAsync_ChecksSharingAProjectGuid_ResolveThatGuidOnce()
    {
        GivenBuildDetails(BuildFor("internal", BuildStatus.Completed, BuildResult.Failed));
        GivenPrChecks(
            AzurePipelinesCheck("net - core - ci", "FAILURE", DetailsUrlFor(5209385, ProjectGuid)),
            AzurePipelinesCheck("net - template - ci", "FAILURE", DetailsUrlFor(5209386, ProjectGuid)));

        await helper.ResolveBuildsAsync(PrUrl, ct: CancellationToken.None);

        devOpsService.Verify(
            d => d.GetBuildDetailsAsync(It.IsAny<int>(), ProjectGuid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResolveCommitRefFromBuildsAsync

    [Test]
    public async Task ResolveCommitRefFromBuildsAsync_BuildBackedByGitHub_ReturnsItsCommitRef()
    {
        var expected = new GitHubCommitRef("Azure", "azure-sdk-for-net", "0f1a2b3c", 44941);
        GivenBuildCommitRef(BuildId, expected);

        var commitRef = await helper.ResolveCommitRefFromBuildsAsync([Resolved(BuildId)], CancellationToken.None);

        Assert.That(commitRef, Is.EqualTo(expected));
    }

    [Test]
    public async Task ResolveCommitRefFromBuildsAsync_FirstBuildCannotBeRead_FallsBackToTheNextBuild()
    {
        var expected = new GitHubCommitRef("Azure", "azure-sdk-for-net", "0f1a2b3c", 44941);
        devOpsService
            .Setup(d => d.ResolveBuildCommitRefAsync(5209385, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("no access"));
        GivenBuildCommitRef(5209386, expected);

        var commitRef = await helper.ResolveCommitRefFromBuildsAsync(
            [Resolved(5209385), Resolved(5209386)], CancellationToken.None);

        Assert.That(commitRef, Is.EqualTo(expected));
    }

    [Test]
    public async Task ResolveCommitRefFromBuildsAsync_FirstBuildHasNoCommit_FallsBackToTheNextBuild()
    {
        var expected = new GitHubCommitRef("Azure", "azure-sdk-for-net", "0f1a2b3c", 44941);
        GivenBuildCommitRef(5209385, null);
        GivenBuildCommitRef(5209386, expected);

        var commitRef = await helper.ResolveCommitRefFromBuildsAsync(
            [Resolved(5209385), Resolved(5209386)], CancellationToken.None);

        Assert.That(commitRef, Is.EqualTo(expected));
    }

    [Test]
    public async Task ResolveCommitRefFromBuildsAsync_NoBuildBackedByGitHub_ReturnsNull()
    {
        GivenBuildCommitRef(BuildId, null);

        var commitRef = await helper.ResolveCommitRefFromBuildsAsync([Resolved(BuildId)], CancellationToken.None);

        Assert.That(commitRef, Is.Null);
    }

    #endregion

    #region Arrange helpers

    private void GivenCurrentRepo(string fullName) =>
        gitHelper
            .Setup(g => g.GetRepoFullNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fullName);

    private void GivenBuildDetails(Build build) =>
        devOpsService
            .Setup(d => d.GetBuildDetailsAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(build);

    private void GivenPrChecks(params PrCheckRun[] checks) =>
        gitHubService
            .Setup(g => g.GetPrCheckRunsAsync("Azure", "azure-sdk-for-net", 44941, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks.ToList());

    private void GivenBuildCommitRef(int buildId, GitHubCommitRef? commitRef) =>
        devOpsService
            .Setup(d => d.ResolveBuildCommitRefAsync(buildId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commitRef);

    private static Build BuildFor(string project, BuildStatus? status = null, BuildResult? result = null) =>
        new()
        {
            Project = new TeamProjectReference { Name = project },
            Status = status,
            Result = result,
        };

    private static AzurePipelineBuild Resolved(int buildId) => new(buildId, "public", null, null, null);

    private static string DetailsUrlFor(int buildId, string project = "public") =>
        $"https://dev.azure.com/azure-sdk/{project}/_build/results?buildId={buildId}";

    private static PrCheckRun AzurePipelinesCheck(string name, string conclusion, string? detailsUrl) =>
        new() { Name = name, Conclusion = conclusion, AppName = "Azure Pipelines", DetailsUrl = detailsUrl, Type = "CheckRun" };

    #endregion
}
