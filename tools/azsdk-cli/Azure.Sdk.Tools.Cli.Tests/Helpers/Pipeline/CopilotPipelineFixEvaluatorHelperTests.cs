// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers.Pipeline;

/// <summary>
/// Seam: <see cref="CopilotPipelineFixEvaluatorHelper.EvaluatePipelineFixesAsync"/> turns a repository and a
/// time window into one telemetry row per Copilot pipeline-fix attempt, given <see cref="IGitHubService"/>
/// and <see cref="IPipelineFixSurvivalJudge"/>. Two delivery paths feed it: an @copilot mention that pushes
/// commits onto the pull request, and the auto-fix workflow that opens a separate copilot-pipeline-fix/ pull
/// request.
/// </summary>
[TestFixture]
public class CopilotPipelineFixEvaluatorHelperTests
{
    private const string Owner = "ReilleyMilne";
    private const string Repo = "azure-sdk-for-python";
    private const string CheckName = "ReilleyMilne.azure-sdk-for-python - pullrequest (Analyze Analyze)";
    private const string FixBranchPrefix = "copilot-pipeline-fix/";

    // Real shapes captured from ReilleyMilne/azure-sdk-for-python#36, the auto-fix pull request opened for #35.
    private const int OriginalPrNumber = 35;
    private const int FixPrNumber = 36;
    private const string FailingSha = "f8eedcaef0bed15bc45b7b620d28461958311c95";
    private const string FixHeadSha = "e7f1ea3de721a53bd3e0c0f83173e2b83455ac75";
    private const string FixBranchRef =
        "copilot-pipeline-fix/pr-35-f8eedcaef0bed15bc45b7b620d28461958311c95/run-30861672656/copilot-pipeline-fix/fix-analyze-pr35-68fe750a7ab81983";

    private static readonly DateTimeOffset Since = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = new(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Merged = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private Mock<IGitHubService> gitHubService;
    private Mock<IPipelineFixSurvivalJudge> survivalJudge;
    private CopilotPipelineFixEvaluatorHelper helper;

    [SetUp]
    public void SetUp()
    {
        gitHubService = new Mock<IGitHubService>(MockBehavior.Loose);
        survivalJudge = new Mock<IPipelineFixSurvivalJudge>(MockBehavior.Loose);
        helper = new CopilotPipelineFixEvaluatorHelper(
            gitHubService.Object,
            survivalJudge.Object,
            new TestLogger<CopilotPipelineFixEvaluatorHelper>());

        gitHubService
            .Setup(g => g.GetMergedPullRequestsByTimeFrameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequest>());
        gitHubService
            .Setup(g => g.GetPullRequestsByHeadPrefixAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
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

        // The recovered path routes through the survival judge; a verified fix is the default so a test only
        // arranges the judge when the survival verdict itself is what it is checking.
        survivalJudge
            .Setup(j => j.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<PullRequestCommit>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CopilotFixVerification.CopilotVerifiedFix, (PipelineFixEvaluationJudgeVerdict?)null));
    }

    #region EvaluatePipelineFixesAsync (top level)

    [Test]
    public async Task EvaluatePipelineFixesAsync_NoMergedPullRequests_ReturnsEmptyWithoutSearchingFixPullRequests()
    {
        var results = await RunAsync();

        Assert.That(results, Is.Empty);
        gitHubService.Verify(
            g => g.GetPullRequestsByHeadPrefixAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    // The fix pull request can be opened after the reporting window closes, so the search for it starts from
    // the earliest merged pull request's creation date rather than the window start.
    [Test]
    public async Task EvaluatePipelineFixesAsync_SearchesFixPullRequestsFromEarliestMergedCreation()
    {
        var earliest = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        GivenMergedPrs(
            MergedPr(10, createdAt: new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero)),
            MergedPr(20, createdAt: earliest));

        await RunAsync();

        gitHubService.Verify(
            g => g.GetPullRequestsByHeadPrefixAsync(Owner, Repo, FixBranchPrefix, earliest, It.IsAny<CancellationToken>()),
            Times.Once);
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

    // A copilot-pipeline-fix/ head belongs to the workflow path; treating it as a mention too would count the
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
            Assert.That(row.FixPrNumber, Is.Null);
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.ChecksBroken, Is.Empty);
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
            Assert.That(row.CopilotCommitShas, Is.EqualTo(new[] { Sha(2) }));
            Assert.That(row.AnalysisCommentPresent, Is.False);
        });
    }

    // A regression means the attempt failed regardless of what else it fixed, and there is no fix left whose
    // survival is worth judging.
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
        survivalJudge.Verify(
            j => j.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<PullRequestCommit>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

    // Only human commits that genuinely landed after the fix are weighed against it: a merge commit pulls in
    // main and a later Copilot commit is not a human override, so both are excluded from what the judge sees.
    [Test]
    public async Task MentionFix_SurvivingHumanCommit_PassedToJudgeExcludingMergeAndCopilotCommits()
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

        IReadOnlyList<PullRequestCommit>? landings = null;
        survivalJudge
            .Setup(j => j.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<PullRequestCommit>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, IReadOnlyList<string>, IReadOnlyList<PullRequestCommit>, string?, CancellationToken>(
                (_, _, _, _, human, _, _) => landings = human)
            .ReturnsAsync((CopilotFixVerification.CopilotJudgeVerifiedFix, JudgeVerdict()));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(landings!.Select(c => c.Sha), Is.EqualTo(new[] { Sha(3) }));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotJudgeVerifiedFix));
            Assert.That(row.JudgeVerdict, Is.Not.Null);
        });
    }

    #endregion

    #region auto-fix workflow path

    [Test]
    public async Task WorkflowFix_FixBranchResolvesToMergedOriginal_ProducesWorkflowRow()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber, title: "Demo: end-to-end pipeline analysis"));
        GivenFixPrs(FixPr(mergedAt: Merged));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.PrNumber, Is.EqualTo(OriginalPrNumber));
            Assert.That(row.FixPrNumber, Is.EqualTo(FixPrNumber));
            Assert.That(row.Trigger, Is.EqualTo(CopilotFixTrigger.GitHubActionsWorkflow));
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
            Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotVerifiedFix));
            Assert.That(row.AnalysisCommentPresent, Is.True);
            Assert.That(row.CopilotCommitShas, Is.EqualTo(new[] { FixHeadSha }));
        });
    }

    // The workflow fixed the pipeline on its own pull request, but that pull request was never merged into
    // the original, so the fix was proven but not adopted.
    [Test]
    public async Task WorkflowFix_FixPullRequestNotMerged_VerificationIsFixNotMerged()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber));
        GivenFixPrs(FixPr(mergedAt: null));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var row = (await RunAsync()).Single();

        Assert.That(row.Verification, Is.EqualTo(CopilotFixVerification.CopilotFixNotMerged));
    }

    [Test]
    public async Task WorkflowFix_OriginalPullRequestNotAmongMerged_ProducesNoRow()
    {
        GivenMergedPrs(MergedPr(999));
        GivenFixPrs(FixPr(mergedAt: Merged));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        GivenChecks(FixHeadSha, Check(CheckName, "SUCCESS"));

        var results = await RunAsync();

        Assert.That(results, Is.Empty);
    }

    // After a fix pull request is retargeted, its live checks re-run against the new base and no longer
    // reflect the result that gated the fix. The bot records the gating checks in a comment keyed to the fix
    // head SHA; that record must win over the live checks.
    [Test]
    public async Task WorkflowFix_EvidenceCommentForHead_PreferredOverLiveChecks()
    {
        GivenMergedPrs(MergedPr(OriginalPrNumber));
        GivenFixPrs(FixPr(mergedAt: null));
        GivenChecks(FailingSha, Check(CheckName, "FAILURE"));
        // Live checks on the retargeted head show the check red; if these were used, no fix would register.
        GivenChecks(FixHeadSha, Check(CheckName, "FAILURE"));
        GivenComments(FixPrNumber, Comment(EvidenceComment(FixHeadSha), "github-actions[bot]"));

        var row = (await RunAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.ChecksFixed, Is.EqualTo(new[] { CheckName }));
            Assert.That(row.PipelineOutcome, Is.EqualTo(CopilotPipelineOutcome.CopilotPipelineFixSuccess));
        });
    }

    #endregion

    #region Arrange helpers

    private Task<List<CopilotPipelineFixResult>> RunAsync() =>
        helper.EvaluatePipelineFixesAsync(Owner, Repo, Since, Until, model: null, CancellationToken.None);

    private void GivenMergedPrs(params PullRequest[] prs) =>
        gitHubService
            .Setup(g => g.GetMergedPullRequestsByTimeFrameAsync(Owner, Repo, Since, Until, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

    private void GivenFixPrs(params PullRequest[] prs) =>
        gitHubService
            .Setup(g => g.GetPullRequestsByHeadPrefixAsync(Owner, Repo, FixBranchPrefix, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

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

    private static PullRequest MergedPr(int number, string? title = null, DateTimeOffset? createdAt = null) =>
        Pr(number, title: title ?? $"PR {number}", mergedAt: Merged, headRef: "user/feature", createdAt: createdAt ?? Since);

    private static PullRequest FixPr(DateTimeOffset? mergedAt) =>
        Pr(FixPrNumber, title: "[pipeline-fix] Fix Analyze stage failure", mergedAt: mergedAt, headRef: FixBranchRef, headSha: FixHeadSha, createdAt: Merged);

    private static PrCheckRun Check(string name, string conclusion) =>
        new() { Name = name, Conclusion = conclusion, Type = "CheckRun" };

    private static PipelineFixEvaluationJudgeVerdict JudgeVerdict() =>
        new()
        {
            CopilotContributionSurvived = true,
            CopilotFixAddressedPipelineFailure = true,
            HumanChangesWereIrrelevantToFix = true,
            Reasoning = "test verdict",
        };

    // The evidence comment the auto-fix workflow posts, tagged with its hidden marker and the fix head SHA,
    // with the gating check rendered as a markdown table row exactly as the bot writes it.
    private static string EvidenceComment(string headSha) =>
        $"""
        <!-- pipeline-analysis-ci-evidence -->
        ### CI results before retargeting

        Commit `{headSha}`, checked while this pull request targeted `main`.

        | Status | Check | Reporter | Time |
        | --- | --- | --- | --- |
        | ✅ success | [{CheckName}](https://dev.azure.com/reilleymilne-test2/_build/results?buildId=63) | Azure Pipelines | 2026-08-03T23:25 |
        | ⚠️ action_required | [Verify Links](https://github.com/ReilleyMilne/azure-sdk-for-python/actions) | GitHub Actions | 2026-08-03T23:23 |
        """;

    private static string Sha(int seed) => seed.ToString("x", System.Globalization.CultureInfo.InvariantCulture).PadLeft(40, '0');

    private static PullRequest Pr(
        int number,
        string? title = null,
        DateTimeOffset? mergedAt = null,
        string? headRef = null,
        string? headSha = null,
        DateTimeOffset createdAt = default)
    {
        var head = new GitReference(null, null, headRef, headRef, headSha, null, null);
        return new PullRequest(
            0L, null, null, null, null, null, null, null, number, ItemState.Closed, title, null,
            createdAt, default, null, mergedAt, head, null, null, null, null, false, null, null, null,
            null, 0, 0, 0, 0, 0, null, false, null, null, null, null, null);
    }

    private static IssueComment Comment(string body, string login) =>
        new(0L, null, null, null, body, default, null, UserFor(login), null, AuthorAssociation.Contributor);

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
