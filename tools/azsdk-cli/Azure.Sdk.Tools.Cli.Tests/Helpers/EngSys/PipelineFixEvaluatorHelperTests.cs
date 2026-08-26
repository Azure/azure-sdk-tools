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

/// <summary>
/// Seam: <see cref="PipelineFixEvaluatorHelper.EvaluatePipelineFixesAsync"/> turns a repository and a
/// time window into one telemetry row per Copilot pipeline-fix attempt, given <see cref="IGitHubService"/>
///. Two delivery paths feed it: an @copilot mention that pushes commits onto the pull request, and the
/// auto-fix workflow that publishes a separate pipeline-fix/ branch.
/// </summary>
[TestFixture]
public class PipelineFixEvaluatorHelperTests
{
    private const string Owner = "ReilleyMilne";
    private const string Repo = "azure-sdk-for-python";
    private const string CheckName = "ReilleyMilne.azure-sdk-for-python - pullrequest (Analyze Analyze)";
    private const int OriginalPrNumber = 35;
    private const string FailingSha = "f8eedcaef0bed15bc45b7b620d28461958311c95";
    private const string FixHeadSha = "e7f1ea3de721a53bd3e0c0f83173e2b83455ac75";
    private const string FixBranchRef =
        "pipeline-fix/pr-35-f8eedcaef0bed15bc45b7b620d28461958311c95/run-30861672656";

    private static readonly DateTimeOffset Since = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Merged = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private Mock<IGitHubService> gitHubService;
    private PipelineFixEvaluatorHelper helper;

    [SetUp]
    public void SetUp()
    {
        gitHubService = new Mock<IGitHubService>(MockBehavior.Loose);
        helper = new PipelineFixEvaluatorHelper(
            gitHubService.Object,
            new TestLogger<PipelineFixEvaluatorHelper>());

        gitHubService
            .Setup(g => g.GetMergedPullRequestsByTimeFrameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequest>());
        gitHubService
            .Setup(g => g.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IssueComment>());
        gitHubService
            .Setup(g => g.GetPullRequestCommitsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestCommit>());
        gitHubService
            .Setup(g => g.GetCommitCheckRunsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PrCheckRun>());
        gitHubService
            .Setup(g => g.GetCommitFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitHubCommitFile>());

    }

    #region EvaluatePipelineFixesAsync (top level)

    [Test]
    public async Task EvaluatePipelineFixesAsync_NoMergedPullRequests_ReturnsEmpty()
    {
        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    // One pull request that blows up mid-evaluation must not sink the whole run.
    [Test]
    public async Task EvaluatePipelineFixesAsync_OnePullRequestThrows_IsSkippedAndOthersStillEvaluated()
    {
        GivenMergedPrs(MergedPr(10), MergedPr(20));
        gitHubService
            .Setup(g => g.GetPullRequestIssueCommentsAsync(Owner, Repo, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("comments unavailable", System.Net.HttpStatusCode.ServiceUnavailable));
        GivenMentionSuccess(20);

        var results = await RunAsync();

        Assert.That(results.Select(r => r.PrNumber), Is.EqualTo(new[] { 20 }));
    }

    [Test]
    public async Task EvaluatePipelineFixesAsync_ManyResults_OrderedByPullRequestNumber()
    {
        GivenMergedPrs(MergedPr(50), MergedPr(20));
        GivenMentionSuccess(50);
        GivenMentionSuccess(20);

        var results = await RunAsync();

        Assert.That(results.Select(r => r.PrNumber), Is.EqualTo(new[] { 20, 50 }));
    }

    #endregion

    #region @copilot mention path

    [Test]
    public async Task MentionFix_NoCopilotMentionComment_ProducesNoRowAndDoesNotFetchCommits()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("please take a look when you can", "human-dev"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
        gitHubService.Verify(
            g => g.GetPullRequestCommitsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Copilot ignores a mention posted by another bot, so an @copilot from a bot login must not count as a
    // human directing Copilot at the pull request.
    [Test]
    public async Task MentionFix_CopilotMentionFromBot_IsIgnored()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "some-automation[bot]"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task MentionFix_CopilotMentionWithMissingAuthor_IsNotTreatedAsBot()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, CommentWithoutUser("@copilot please fix"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Trigger, Is.EqualTo(CopilotFixTrigger.CopilotMention));
    }

    [Test]
    public async Task MentionFix_MentionButNoCopilotAuthoredCommits_ProducesNoRow()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, HumanCommit(Sha(2), Sha(1)));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    // GitHub truncates the commit listing at 250, so a pull request at the cap may be hiding the commit that
    // undid the fix; it is dropped rather than judged on partial history. The fixture holds a Copilot fix
    // that would otherwise produce a row, so the cap is the only reason the result is empty.
    [Test]
    public async Task MentionFix_CommitListingAtGitHubCap_IsSkipped()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(
            10,
            [.. Enumerable.Range(0, 249).Select(i => HumanCommit(Sha(1000 + i), Sha(1))), CopilotCommit(Sha(2), Sha(1))]);
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    // Copilot's commit is sometimes a merge of origin/main into the branch. Only its first parent is the
    // branch's own baseline; the second is a mainline commit whose checks cover the whole repository and
    // would swamp the handful that actually ran here. Comparing against the wrong parent finds no check
    // that changed state on both sides, so the attempt would vanish instead of registering as a fix.
    [Test]
    public async Task MentionFix_CopilotCommitIsAMerge_ComparesAgainstFirstParentOnly()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotMergeCommit(Sha(2), firstParentSha: Sha(1), secondParentSha: Sha(99)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(99), Check("mainline - unrelated coverage", "SUCCESS"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
        });
        gitHubService.Verify(
            g => g.GetCommitCheckRunsAsync(Owner, Repo, Sha(99), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task MentionFix_MergedAtNull_IsSkippedBeforeFetchingComments()
    {
        GivenMergedPrs(Pr(10, mergedAt: null, headRef: "user/feature"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
        gitHubService.Verify(
            g => g.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // A pipeline-fix/ head belongs to the workflow path; treating it as a mention too would count the
    // same fix twice.
    [Test]
    public async Task MentionFix_HeadIsFixBranch_IsSkippedBeforeFetchingComments()
    {
        GivenMergedPrs(Pr(10, mergedAt: Merged, headRef: FixBranchRef, headSha: FixHeadSha));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
        gitHubService.Verify(
            g => g.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task MentionFix_CopilotCommitTurnsAFailingCheckGreen_ProducesSuccessRow()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.PrNumber, Is.EqualTo(10));
            Assert.That(row.Trigger, Is.EqualTo(CopilotFixTrigger.CopilotMention));
            Assert.That(row.FixBranch, Is.Null);
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.ChecksBroken, Is.Empty);
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
            Assert.That(row.CopilotCommitShas, Is.EqualTo(new[] { Sha(2) }));
            Assert.That(row.AnalysisCommentPresent, Is.False);
        });
    }

    // A regression means the attempt failed regardless of what else it fixed.
    [Test]
    public async Task MentionFix_CopilotCommitBreaksAPassingCheck_ProducesFailureRowWithoutJudging()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "SUCCESS"));
        GivenChecks(Sha(2), Check(CheckName, "FAILURE"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixFailure));
            Assert.That(row.ChecksBroken, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.NotApplicable));
        });
    }

    // A pass -> fail flip on a check that is itself still red on the commit that merged is a known failure the
    // team accepted, not a regression the fix is answerable for. This mirrors the real case where Copilot's
    // commit turned one check green while a second check - red at merge and merged anyway - flipped alongside it.
    [Test]
    public async Task MentionFix_BrokenCheckStillRedOnMergedHead_IsDiscountedSoOutcomeIsSuccess()
    {
        const string RustCheck = "SDK Validation - Rust";
        const string ApiDocCheck = "Swagger ApiDocPreview";

        // The merge commit has the same check result as the Copilot source head.
        GivenMergedPrs(MergedPr(10, headSha: Sha(98), mergeCommitSha: Sha(2)));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(RustCheck, "FAILURE"), Check(ApiDocCheck, "SUCCESS"));
        GivenChecks(Sha(2), Check(RustCheck, "SUCCESS"), Check(ApiDocCheck, "FAILURE"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { RustCheck }));
            Assert.That(row.ChecksBroken, Is.Empty);
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
        });
    }

    // The discount only applies to checks red at merge: a check Copilot broke that a later human commit turned
    // green before merge is not a known failure the team accepted, so it still counts as a regression.
    [Test]
    public async Task MentionFix_BrokenCheckGreenOnMergedHead_StillCountsAsFailure()
    {
        GivenMergedPrs(MergedPr(10, headSha: Sha(3), mergeCommitSha: Sha(99)));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)), HumanCommit(Sha(3), Sha(2)));
        GivenChecks(Sha(1), Check(CheckName, "SUCCESS"));
        GivenChecks(Sha(2), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(99), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.ChecksBroken, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixFailure));
        });
    }

    // A check that was already red and stayed red says nothing about the fix, so no row is emitted: the
    // absence of evidence is distinct from a success or a failure.
    [Test]
    public async Task MentionFix_NoCheckChangedState_ProducesNoRow()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "FAILURE"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    // The failure-analysis comment is posted by the bot; the human's @copilot is a separate comment. Both
    // being present is what marks the row as analysis-driven.
    [Test]
    public async Task MentionFix_AnalysisComment_SetsAnalysisCommentPresent()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(
            10,
            Comment("[Pilot] PR Pipeline Failure Analysis\n\nThe Analyze stage failed.", "github-actions[bot]"),
            Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.That(row.AnalysisCommentPresent, Is.True);
    }

    [Test]
    public async Task MentionFix_HumanCommitAfterFix_RemainsCheckBasedSuccess()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(
            10,
            CopilotCommit(Sha(2), Sha(1)),
            HumanCommit(Sha(3), Sha(2)),
            MergeCommit(Sha(4), Sha(2), Sha(99)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task MentionFix_HumanCommitTouchesSameChangedLine_IsOverridden()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)), HumanCommit(Sha(3), Sha(2)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));
        GivenCommitFiles(Sha(2), CommitFile("src/demo.py", "@@ -10,1 +10,1 @@\n-old\n+copilot"));
        GivenCommitFiles(Sha(3), CommitFile("src/demo.py", "@@ -10,1 +10,1 @@\n-copilot\n+human"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixOverridden));
    }

    [Test]
    public async Task MentionFix_HumanCommitTouchesDifferentLine_RemainsVerified()
    {
        GivenMergedPrs(MergedPr(10));
        GivenComments(10, Comment("@copilot please fix", "human-dev"));
        GivenCommits(10, CopilotCommit(Sha(2), Sha(1)), HumanCommit(Sha(3), Sha(2)));
        GivenChecks(Sha(1), Check(CheckName, "FAILURE"));
        GivenChecks(Sha(2), Check(CheckName, "SUCCESS"));
        GivenCommitFiles(Sha(2), CommitFile("src/demo.py", "@@ -10,1 +10,1 @@\n-old\n+copilot"));
        GivenCommitFiles(Sha(3), CommitFile("src/demo.py", "@@ -30,1 +30,1 @@\n-old\n+human"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    #endregion

    #region auto-fix workflow path

    [Test]
    public async Task WorkflowFix_FixBranchResolvesToMergedOriginal_ProducesWorkflowRow()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber, title: "Demo: end-to-end pipeline analysis", headSha: FixHeadSha));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.PrNumber, Is.EqualTo(OriginalPrNumber));
            Assert.That(row.FixBranch, Is.EqualTo(FixBranchRef));
            Assert.That(row.Trigger, Is.EqualTo(CopilotFixTrigger.GitHubActionsWorkflow));
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
            Assert.That(row.AnalysisCommentPresent, Is.True);
            Assert.That(row.CopilotCommitShas, Is.EqualTo(new[] { FixHeadSha }));
        });
    }

    [Test]
    public async Task WorkflowFix_FixedCheckAbsentAtMergedHead_RemainsVerified()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber, headSha: Sha(98), mergeCommitSha: Sha(3)));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(FixHeadSha, FailingSha), MergeCommit(Sha(3), FixHeadSha, Sha(99)));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));
        GivenChecks(Sha(3));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task WorkflowFix_FixedCheckFailingAtMergedHead_IsOverridden()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber, headSha: Sha(98), mergeCommitSha: Sha(3)));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(FixHeadSha, FailingSha), MergeCommit(Sha(3), FixHeadSha, Sha(99)));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));
        GivenChecks(Sha(3), Check(CheckName, "FAILURE"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixOverridden));
    }

    // The workflow published a branch, but it never entered the original pull request, so its pipeline
    // behavior is not evaluated.
    [Test]
    public async Task WorkflowFix_BranchNotAdopted_VerificationIsFixNotMerged()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixNotMerged));
            Assert.That(row.PipelineOutcome, Is.Null);
            Assert.That(row.ChecksFixed, Is.Empty);
            Assert.That(row.ChecksBroken, Is.Empty);
        });
        gitHubService.Verify(
            g => g.GetCommitCheckRunsAsync(Owner, Repo, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task WorkflowFix_EquivalentPatchAppliedWithNewSha_RemainsVerified()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber, headSha: Sha(3)));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(Sha(3), FailingSha));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));
        GivenChecks(Sha(3));
        const string patch = "@@ -1 +0,0 @@\n-Intentional pipeline failure marker";
        GivenCommitFiles(FixHeadSha, CommitFile("sdk/core/azure-core/ci-fail-marker.txt", patch, "removed"));
        GivenCommitFiles(Sha(3), CommitFile("sdk/core/azure-core/ci-fail-marker.txt", patch, "removed"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
    }

    [Test]
    public async Task WorkflowFix_OriginalPullRequestNotAmongMerged_ProducesNoRow()
    {
        GivenMergedPrs(MergedPr(999));
        GivenComments(999, WorkflowComment());
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task WorkflowFix_DeletedBranch_DoesNotSuppressLaterAttempt()
    {
        var deletedBranch = $"pipeline-fix/pr-{OriginalPrNumber}-{FailingSha}/run-1";
        var comment = Comment(
            "[Pilot] PR Pipeline Failure Analysis\n"
            + $"https://github.com/{Owner}/{Repo}/compare/user%2Ffeature...{deletedBranch}\n"
            + $"https://github.com/{Owner}/{Repo}/compare/user%2Ffeature...{FixBranchRef}",
            "github-actions[bot]");
        GivenMergedPrs(MergedPr(OriginalPrNumber, mergeCommitSha: FixHeadSha));
        GivenComments(OriginalPrNumber, comment);
        gitHubService
            .Setup(g => g.GetBranchHeadShaAsync(Owner, Repo, deletedBranch, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("branch deleted", System.Net.HttpStatusCode.NotFound));
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(FixHeadSha, FailingSha));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.That(row.FixBranch, Is.EqualTo(FixBranchRef));
    }

    [Test]
    public async Task WorkflowFix_TruncatedCommitList_ProducesNoClassification()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        var commits = Enumerable.Range(1, 250)
            .Select(index => HumanCommit(Sha(index + 100), index == 1 ? FailingSha : Sha(index + 99)))
            .ToArray();
        GivenCommits(OriginalPrNumber, commits);

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
        gitHubService.Verify(
            g => g.GetCommitCheckRunsAsync(Owner, Repo, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task WorkflowFix_HumanCommitTouchesFixLineButDoesNotApplyPatch_IsNotMerged()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber));
        GivenComments(OriginalPrNumber, WorkflowComment());
        GivenBranchHead(FixBranchRef, FixHeadSha);
        GivenCommits(OriginalPrNumber, HumanCommit(Sha(3), FailingSha));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));
        GivenCommitFiles(FixHeadSha, CommitFile("src/demo.py", "@@ -10,1 +10,1 @@\n-old\n+copilot"));
        GivenCommitFiles(Sha(3), CommitFile("src/demo.py", "@@ -10,1 +10,1 @@\n-copilot\n+human"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixNotMerged));
            Assert.That(row.PipelineOutcome, Is.Null);
        });
    }

    #endregion

    #region Arrange helpers

    private Task<List<CopilotPipelineFixResult>> RunAsync() =>
        helper.EvaluatePipelineFixesAsync(Owner, Repo, Since, Until, CancellationToken.None);

    private void GivenMergedPrs(params PullRequest[] prs) =>
        gitHubService
            .Setup(g => g.GetMergedPullRequestsByTimeFrameAsync(Owner, Repo, Since, Until, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

    private void GivenBranchHead(string branch, string sha) =>
        gitHubService
            .Setup(g => g.GetBranchHeadShaAsync(Owner, Repo, branch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sha);

    private void GivenComments(int prNumber, params IssueComment[] comments) =>
        gitHubService
            .Setup(g => g.GetPullRequestIssueCommentsAsync(Owner, Repo, prNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);

    private void GivenCommits(int prNumber, params PullRequestCommit[] commits) =>
        gitHubService
            .Setup(g => g.GetPullRequestCommitsAsync(Owner, Repo, prNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

    private void GivenChecks(string sha, params PrCheckRun[] checks) =>
        gitHubService
            .Setup(g => g.GetCommitCheckRunsAsync(Owner, Repo, sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(checks);

    private void GivenCommitFiles(string sha, params GitHubCommitFile[] files) =>
        gitHubService
            .Setup(g => g.GetCommitFilesAsync(Owner, Repo, sha, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

    // A complete @copilot mention fix that turns CheckName from red to green, using per-PR distinct SHAs.
    private void GivenMentionSuccess(int prNumber)
    {
        var before = Sha(prNumber * 10 + 1);
        var after = Sha(prNumber * 10 + 2);
        GivenComments(prNumber, Comment("@copilot please fix", "human-dev"));
        GivenCommits(prNumber, CopilotCommit(after, before));
        GivenChecks(before, Check(CheckName, "FAILURE"));
        GivenChecks(after, Check(CheckName, "SUCCESS"));
    }

    private static PullRequest MergedPr(
        int number,
        string? title = null,
        DateTimeOffset? createdAt = null,
        string? headSha = null,
        string? mergeCommitSha = null) =>
        Pr(
            number,
            title: title ?? $"PR {number}",
            mergedAt: Merged,
            headRef: "user/feature",
            headSha: headSha,
            mergeCommitSha: mergeCommitSha,
            createdAt: createdAt ?? Since);

    private static PrCheckRun Check(string name, string conclusion) =>
        new() { Name = name, Conclusion = conclusion, Type = "CheckRun" };

    private static IssueComment WorkflowComment() => Comment(
        $"[Pilot] PR Pipeline Failure Analysis\n\n**Automated fix:** [Fix found, view and apply fix](https://github.com/{Owner}/{Repo}/compare/user%2Ffeature...{FixBranchRef})",
        "github-actions[bot]");

    private static GitHubCommitFile CommitFile(string filename, string patch, string status = "modified") =>
        new(filename, 1, 1, 2, status, null, null, null, Sha(999), patch, null);

    private static string Sha(int seed) => seed.ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(40, '0');

    private static PullRequest Pr(
        int number,
        string? title = null,
        DateTimeOffset? mergedAt = null,
        string? headRef = null,
        string? headSha = null,
        string? mergeCommitSha = null,
        DateTimeOffset createdAt = default)
    {
        var head = new GitReference(null, null, headRef, headRef, headSha, null, null);
        return new PullRequest(
            0L, null, null, null, null, null, null, null, number, ItemState.Closed, title, null,
            createdAt, default, null, mergedAt, head, null, null, null, null, false, null, null, null,
            mergeCommitSha, 0, 0, 0, 0, 0, null, false, null, null, null, null, null);
    }

    private static IssueComment Comment(string body, string login) =>
        new(0L, null, null, null, body, default, null, UserFor(login), null, AuthorAssociation.Contributor);

    private static IssueComment CommentWithoutUser(string body) =>
        new(0L, null, null, null, body, default, null, null, null, AuthorAssociation.Contributor);

    /// A commit that passes the evaluator's IsCopilotAuthored gate: Copilot as author, GitHub as committer,
    /// and a verified signature.
    private static PullRequestCommit CopilotCommit(string sha, string parentSha) =>
        PrCommit(sha, [parentSha], "Copilot", "GitHub", verified: true);

    private static PullRequestCommit HumanCommit(string sha, string parentSha, string login = "dev") =>
        PrCommit(sha, [parentSha], login, login, verified: false);

    private static PullRequestCommit MergeCommit(string sha, string firstParentSha, string secondParentSha, string login = "dev") =>
        PrCommit(sha, [firstParentSha, secondParentSha], login, login, verified: false);

    /// A Copilot commit that merged the mainline back into the branch, so it carries two parents.
    private static PullRequestCommit CopilotMergeCommit(string sha, string firstParentSha, string secondParentSha) =>
        PrCommit(sha, [firstParentSha, secondParentSha], "Copilot", "GitHub", verified: true);

    /// <summary>
    /// A commit as it appears in a pull request's commit listing. Octokit response models expose only an
    /// all-argument constructor, so the fields the evaluator inspects - the SHA, its parents, and the author
    /// and committer identity that mark a commit as Copilot-authored - are named and the rest are defaulted.
    /// </summary>
    private static PullRequestCommit PrCommit(
        string sha,
        IEnumerable<string> parentShas,
        string authorLogin,
        string committerName,
        bool verified)
    {
        var commit = new Commit(
            null, null, null, null, sha, null, null, "message",
            new Committer(authorLogin, "author@example.com", default),
            new Committer(committerName, "committer@example.com", default),
            null, Array.Empty<GitReference>(), 0,
            new Verification(verified, VerificationReason.Valid, null, null));

        return new PullRequestCommit(
            null, UserFor(authorLogin), null, commit, null, null, parentShas.Select(Ref).ToArray(), sha, null);
    }

    private static User UserFor(string login) =>
        new(null, null, null, 0, null, default, default, 0, null, 0, 0, null, null, 0, 0L, null,
            login, null, null, 0, null, 0, 0, 0, null, null, false, null, null);

    private static GitReference Ref(string sha) => new(null, null, null, null, sha, null, null);

    #endregion
}
