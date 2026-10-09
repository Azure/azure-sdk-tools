// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers.EngSys;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.EngSys;

[TestFixture]
public class PipelineFixEvaluatorHelperTests
{
    private const string Owner = "Azure";
    private const string Repo = "azure-sdk-for-python";
    private const string CheckName = "ci";
    private const int PrNumber = 35;
    private const string FailingSha = "f8eedcaef0bed15bc45b7b620d28461958311c95";
    private const string FixHeadSha = "e7f1ea3de721a53bd3e0c0f83173e2b83455ac75";
    private const string FixBranch = "pipeline-fix/pr-35-f8eedcaef0bed15bc45b7b620d28461958311c95/run-30861672656";
    private static readonly DateTimeOffset Since = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = Since.AddDays(1);
    private static readonly DateTimeOffset MergedAt = Since.AddHours(12);

    private Mock<IGitHubService> gitHub = null!;
    private PipelineFixEvaluatorHelper helper = null!;

    [SetUp]
    public void SetUp()
    {
        gitHub = new Mock<IGitHubService>(MockBehavior.Loose);
        helper = new(gitHub.Object, new TestLogger<PipelineFixEvaluatorHelper>());
        GivenMergedPullRequests();
        GivenComments();
        GivenCommits();
        GivenChecks(FailingSha);
        GivenChecks(FixHeadSha);
        GivenCommitFiles(FixHeadSha);
    }

    [Test]
    public async Task MergedPullRequestWithoutWorkflow_OnlyEntersFirstStage()
    {
        GivenMergedPullRequests(PullRequest());

        var result = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.PrNumber, Is.EqualTo(PrNumber));
            Assert.That(result.PrTitle, Is.EqualTo("Fix build"));
            Assert.That(result.FixWorkflowRun, Is.Null);
            Assert.That(result.FixBranchOpened, Is.Null);
            Assert.That(result.FixPullRequestMerged, Is.Null);
            Assert.That(result.PipelineOutcome, Is.Null);
            Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
        });
    }

    [Test]
    public async Task WorkflowRequestedWithoutRunId_RemainsAtMergedStage()
    {
        GivenMergedPullRequests(PullRequest());
        GivenComments(WorkflowComment("Requested"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixWorkflowRun, Is.Null);
        Assert.That(result.FixBranchOpened, Is.Null);
    }

    [Test]
    public async Task OpenedFixBranchNotAdopted_StopsAtOpenedStage()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(Sha(3), FailingSha));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(10, "old", "different")));

        var result = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.FixWorkflowRun, Is.EqualTo(30861672656));
            Assert.That(result.FixBranchOpened, Is.EqualTo(FixBranch));
            Assert.That(result.FixPullRequestMerged, Is.Null);
            Assert.That(result.PipelineOutcome, Is.Null);
            Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixNotMerged));
        });
    }

    [Test]
    public async Task AdoptedBranchThatFixesPipeline_ReachesSuccessStage()
    {
        GivenOpenedFixBranch(mergeCommitSha: FixHeadSha);
        GivenCommits(Commit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.FixWorkflowRun, Is.EqualTo(30861672656));
            Assert.That(result.FixBranchOpened, Is.EqualTo(FixBranch));
            Assert.That(result.FixPullRequestMerged, Is.EqualTo(FixHeadSha));
            Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
            Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
        });
    }

    [Test]
    public async Task LaterHumanCommitOverlappingFix_IsOverridden()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(FixHeadSha, FailingSha), Commit(Sha(3), FixHeadSha));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(10, "fix", "human")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixOverridden));
    }

    [Test]
    public async Task LaterHumanCommitTouchingDifferentLine_RemainsVerified()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(FixHeadSha, FailingSha), Commit(Sha(3), FixHeadSha));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(30, "old", "human")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task MultipleLaterHumanCommitsTouchingDifferentLines_RemainVerified()
    {
        GivenOpenedFixBranch();
        GivenCommits(
            Commit(FixHeadSha, FailingSha),
            Commit(Sha(3), FixHeadSha),
            Commit(Sha(4), Sha(3)));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(30, "old", "first human")));
        GivenCommitFiles(Sha(4), CommitFile(Patch(50, "old", "second human")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task LaterBotCommitOverlappingFix_RemainsVerified()
    {
        GivenOpenedFixBranch();
        GivenCommits(
            Commit(FixHeadSha, FailingSha),
            Commit(Sha(3), FixHeadSha, ActionsBotName));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(10, "fix", "bot")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task FailedCheckAtMergedHeadWithoutHumanRewrite_RemainsVerified()
    {
        GivenOpenedFixBranch(mergeCommitSha: Sha(3));
        GivenCommits(Commit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("SUCCESS"));
        GivenChecks(Sha(3), Check("FAILURE"));

        var result = (await RunAsync()).Single();

        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
        gitHub.Verify(
            service => service.GetCommitCheckRunsAsync(Owner, Repo, Sha(3), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task AdoptedBranchThatDoesNotFixPipeline_RecordsFailure()
    {
        GivenOpenedFixBranch(mergeCommitSha: FixHeadSha);
        GivenCommits(Commit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(FixHeadSha, Check("FAILURE"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(FixHeadSha));
        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixFailure));
        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
    }

    [Test]
    public async Task AdoptedBranchWithoutCheckEvidence_LeavesOutcomeUnknown()
    {
        GivenOpenedFixBranch(mergeCommitSha: FixHeadSha);
        GivenCommits(Commit(FixHeadSha, FailingSha));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(FixHeadSha));
        Assert.That(result.PipelineOutcome, Is.Null);
        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
    }

    [Test]
    public async Task AdoptedBranchWithMissingAfterChecks_RecordsFailure()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check("FAILURE"));

        var result = (await RunAsync()).Single();

        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixFailure));
        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
    }

    [Test]
    public async Task AdoptedBranchThatFixesOneCheckButRegressesAnother_RecordsFailure()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check("analyze", "FAILURE"), Check("verify", "SUCCESS"));
        GivenChecks(FixHeadSha, Check("analyze", "SUCCESS"), Check("verify", "FAILURE"));

        var result = (await RunAsync()).Single();

        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixFailure));
        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
    }

    [Test]
    public async Task ShiftedEquivalentPatchCountsAsAdoptedAndUsesAdoptedChecks()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(Sha(3), FailingSha));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(30, "old", "fix")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(Sha(3), Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(Sha(3)));
        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
        gitHub.Verify(
            service => service.GetCommitCheckRunsAsync(Owner, Repo, FixHeadSha, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task LaterHumanCommitOverlappingShiftedAdoption_IsOverridden()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(Sha(3), FailingSha), Commit(Sha(4), Sha(3)));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "old", "fix")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(30, "old", "fix")));
        GivenCommitFiles(Sha(4), CommitFile(Patch(30, "fix", "human")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(Sha(3), Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(Sha(3)));
        Assert.That(result.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixOverridden));
    }

    [Test]
    public async Task ShiftedAdoptionPreservesChangedLinesBeginningWithRepeatedSigns()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(Sha(3), FailingSha));
        GivenCommitFiles(FixHeadSha, CommitFile(Patch(10, "--count", "++count")));
        GivenCommitFiles(Sha(3), CommitFile(Patch(30, "--count", "++count")));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(Sha(3), Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(Sha(3)));
        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
    }

    [Test]
    public async Task PartialEquivalentPatchCountsAsAdopted()
    {
        GivenOpenedFixBranch();
        GivenCommits(Commit(Sha(3), FailingSha));
        GivenCommitFiles(
            FixHeadSha,
            CommitFile(Patch(10, "old", "fix"), "src/first.cs"),
            CommitFile(Patch(20, "old", "other fix"), "src/second.cs"),
            CommitFile(null, "assets/image.bin"));
        GivenCommitFiles(Sha(3), CommitFile(Patch(40, "old", "fix"), "src/first.cs"));
        GivenChecks(FailingSha, Check("FAILURE"));
        GivenChecks(Sha(3), Check("SUCCESS"));

        var result = (await RunAsync()).Single();

        Assert.That(result.FixPullRequestMerged, Is.EqualTo(Sha(3)));
        Assert.That(result.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
    }

    [Test]
    public async Task FixBranchPullRequestIsExcluded()
    {
        GivenMergedPullRequests(PullRequest(headRef: FixBranch));

        Assert.That(await RunAsync(), Is.Empty);
    }

    [Test]
    public async Task PullRequestsAreOrderedAndFailuresAreIsolated()
    {
        GivenMergedPullRequests(PullRequest(50), PullRequest(20));
        gitHub
            .Setup(service => service.GetPullRequestIssueCommentsAsync(Owner, Repo, 20, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("unavailable", System.Net.HttpStatusCode.ServiceUnavailable));

        var results = await RunAsync();

        Assert.That(results.Select(result => result.PrNumber), Is.EqualTo(new[] { 20, 50 }));
    }

    private Task<List<PipelineFixEvaluation>> RunAsync() =>
        helper.EvaluatePipelineFixesAsync(Owner, Repo, Since, Until, CancellationToken.None);

    private void GivenOpenedFixBranch(string? mergeCommitSha = null)
    {
        GivenMergedPullRequests(PullRequest(mergeCommitSha: mergeCommitSha));
        GivenComments(WorkflowComment($"[Fix](https://github.com/{Owner}/{Repo}/compare/main...{FixBranch})"));
        gitHub
            .Setup(service => service.GetBranchHeadShaAsync(Owner, Repo, FixBranch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FixHeadSha);
    }

    private void GivenMergedPullRequests(params PullRequest[] pullRequests) =>
        gitHub
            .Setup(service => service.GetMergedPullRequestsByTimeFrameAsync(Owner, Repo, Since, Until, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

    private void GivenComments(params IssueComment[] comments) =>
        gitHub
            .Setup(service => service.GetPullRequestIssueCommentsAsync(Owner, Repo, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);

    private void GivenCommits(params PullRequestCommit[] commits) =>
        gitHub
            .Setup(service => service.GetPullRequestCommitsAsync(Owner, Repo, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

    private void GivenChecks(string sha, params PrCheckRun[] checks) =>
        gitHub
            .Setup(service => service.GetCommitCheckRunsAsync(Owner, Repo, sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

    private void GivenCommitFiles(string sha, params GitHubCommitFile[] files) =>
        gitHub
            .Setup(service => service.GetCommitFilesAsync(Owner, Repo, sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

    private static PullRequest PullRequest(
        int number = PrNumber,
        string headRef = "feature",
        string? mergeCommitSha = null)
    {
        var head = new GitReference(null, null, headRef, headRef, Sha(number), null, null);
        return new PullRequest(
            0L, null, null, null, null, null, null, null, number, ItemState.Closed, "Fix build", null,
            Since, default, null, MergedAt, head, null, null, null, null, false, null, null, null,
            mergeCommitSha, 0, 0, 0, 0, 0, null, false, null, null, null, null, null);
    }

    private static IssueComment WorkflowComment(string automatedFix) =>
        new(0L, null, null, null,
            $"[Pilot] PR Pipeline Failure Analysis\n\n**Automated fix:** {automatedFix}",
            default, null, User(ActionsBotName), null, AuthorAssociation.Contributor);

    private const string ActionsBotName = "github-actions[bot]";

    private static PullRequestCommit Commit(string sha, string parentSha, string author = "dev") =>
        new(null, User(author), null, null, null, null, [Reference(parentSha)], sha, null);

    private static PrCheckRun Check(string conclusion) => Check(CheckName, conclusion);

    private static PrCheckRun Check(string name, string conclusion) =>
        new() { Name = name, Conclusion = conclusion, Type = "CheckRun" };

    private static GitHubCommitFile CommitFile(string? patch, string filename = "src/file.cs") =>
        new(filename, 1, 1, 2, "modified", null, null, null, Sha(999), patch, null);

    private static string Patch(int line, string oldValue, string newValue) =>
        $"@@ -{line},1 +{line},1 @@\n-{oldValue}\n+{newValue}";

    private static string Sha(int seed) => seed.ToString("x").PadLeft(40, '0');

    private static User User(string login) =>
        new(null, null, null, 0, null, default, default, 0, null, 0, 0, null, null, 0, 0L, null,
            login, null, null, 0, null, 0, 0, 0, null, null, false, null, null);

    private static GitReference Reference(string sha) => new(null, null, null, null, sha, null, null);
}
