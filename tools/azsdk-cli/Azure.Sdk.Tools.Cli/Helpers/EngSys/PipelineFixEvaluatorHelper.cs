// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers.EngSys;

/// <summary>
/// Measures how often Copilot fixes a failing pipeline in a repository over a window of time.
///
/// Copilot fixes a pipeline in one of two ways: it pushes commits onto the pull request after someone
/// comments @copilot, or its auto-fix workflow opens a separate pull request holding the fix.
/// Take the commit from before the fix and the commit from after it, then see which
/// checks went from failing to passing, and which went the other way.
/// </summary>
public interface IPipelineFixEvaluatorHelper
{
    Task<List<CopilotPipelineFixResult>> EvaluatePipelineFixesAsync(
        string owner,
        string repo,
        DateTimeOffset since,
        DateTimeOffset until,
        string? model,
        CancellationToken ct);
}

public class PipelineFixEvaluatorHelper(
    IGitHubService gitHubService,
    IPipelineFixSurvivalJudge survivalJudge,
    ILogger<PipelineFixEvaluatorHelper> logger
) : IPipelineFixEvaluatorHelper
{
    private const int CommitListLimit = 250;
    private const string CopilotMention = "@copilot";
    private const string AnalysisMarker = "[Pilot] PR Pipeline Failure Analysis";
    private const string BotLoginSuffix = "[bot]";
    private const string GitHubCommitterName = "GitHub";
    private const string ActionsBotName = "github-actions[bot]";
    private const string FixBranchPrefix = "copilot-pipeline-fix/";
    private static readonly string[] CopilotAuthors = ["Copilot", "copilot-swe-agent[bot]"];
    private static readonly Regex FixBranch =
        new(@"^copilot-pipeline-fix/pr-(\d+)-([0-9a-f]{40})/", RegexOptions.IgnoreCase);

    private const string EvidenceMarker = "<!-- pipeline-analysis-ci-evidence -->";
    private static readonly Regex EvidenceCommit = new(@"Commit `([0-9a-f]{40})`", RegexOptions.IgnoreCase);
    private static readonly Regex EvidenceCheckName = new(@"\[([^\]]+)\]");

    private const string Passed = "SUCCESS";
    private static readonly HashSet<string> Failed = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAILURE", "ERROR", "TIMED_OUT", "STARTUP_FAILURE",
    };

    /// <summary>
    /// The checks that ran on one commit, split into the ones that passed and the ones that failed. A check
    /// that is still running, or that was cancelled, skipped or neutral, is neither, so it is left out of both.
    /// </summary>
    private sealed record CheckSet(HashSet<string> Failed, HashSet<string> Passed)
    {
        public int Count => Failed.Count + Passed.Count;
    }

    public async Task<List<CopilotPipelineFixResult>> EvaluatePipelineFixesAsync(
        string owner,
        string repo,
        DateTimeOffset since,
        DateTimeOffset until,
        string? model,
        CancellationToken ct)
    {
        List<CopilotPipelineFixResult> results = [];

        var mergedPrs = await gitHubService.GetMergedPullRequestsByTimeFrameAsync(owner, repo, since, until, ct);
        foreach (var pr in mergedPrs)
        {
            try
            {
                var result = await CollectMentionFixAsync(owner, repo, pr, model, ct);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Skipping pull request {PrNumber}", pr.Number);
            }
        }

        if (mergedPrs.Count == 0)
        {
            return results;
        }

        // Filter the fix PR. The fix PR can exist outside the reporting window,
        // so we need to search for it starting from the earliest merged PR's creation date.
        var originals = mergedPrs.DistinctBy(pr => pr.Number).ToDictionary(pr => pr.Number);
        var fixPrsSince = mergedPrs.Min(pr => pr.CreatedAt);

        foreach (var fixPr in await gitHubService.GetPullRequestsByHeadPrefixAsync(owner, repo, FixBranchPrefix, fixPrsSince, ct))
        {
            try
            {
                var result = await CollectWorkflowFixAsync(owner, repo, fixPr, originals, ct);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Skipping fix pull request {FixPrNumber}", fixPr.Number);
            }
        }

        // Several attempts can share a pull request number, so the fix pull request breaks the tie and keeps
        // the ordering stable from one run to the next.
        return results.OrderBy(r => r.PrNumber).ThenBy(r => r.FixPrNumber ?? 0).ToList();
    }

    // Handles the @copilot mention case. Copilot pushed its fix straight onto this pull request, so the
    // commit before the fix is the parent of each Copilot commit.
    private async Task<CopilotPipelineFixResult?> CollectMentionFixAsync(
        string owner,
        string repo,
        PullRequest pr,
        string? model,
        CancellationToken ct)
    {
        if (pr.MergedAt == null || FixBranch.IsMatch(pr.Head?.Ref ?? string.Empty))
        {
            return null;
        }

        // A person has to have written the @copilot comment, because Copilot ignores a mention from another
        // bot. The failure-analysis comment is recorded but not required, so a hand-written mention counts too.
        // This gate runs before the commit fetch because it rejects most pull requests for the same one call.
        var comments = await gitHubService.GetPullRequestIssueCommentsAsync(owner, repo, pr.Number, ct);
        if (!comments.Any(c => Mentions(c, CopilotMention) && !IsBotComment(c)))
        {
            return null;
        }

        var commits = await gitHubService.GetPullRequestCommitsAsync(owner, repo, pr.Number, ct);

        // GitHub stops listing at 250 commits, and the missing ones could hold the work that undid the fix.
        if (commits.Count >= CommitListLimit)
        {
            logger.LogWarning(
                "Skipping pull request {PrNumber}: it has at least {CommitListLimit} commits, which is as many as GitHub will list.",
                pr.Number, CommitListLimit);
            return null;
        }

        var copilotCommits = commits.Where(IsCopilotAuthored).ToList();
        if (copilotCommits.Count == 0)
        {
            return null;
        }

        var afterShas = copilotCommits.Select(c => c.Sha).ToList();
        var landedOnTop = CommitsAfter(afterShas, commits);
        var fixOrLater = new HashSet<string>(
            afterShas.Concat(landedOnTop.Select(c => c.Sha)), StringComparer.OrdinalIgnoreCase);

        // Only the first parent is used. If a Copilot commit is a merge, its second parent is a commit on
        // main, and the checks on main cover the whole repository. Those hundreds of unrelated checks would
        // bury the handful that actually ran on this branch.
        List<string> beforeShas = [];
        foreach (var commit in copilotCommits)
        {
            var parentSha = commit.Parents?.FirstOrDefault()?.Sha;
            if (string.IsNullOrEmpty(parentSha)
                || fixOrLater.Contains(parentSha)
                || beforeShas.Contains(parentSha, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            beforeShas.Add(parentSha);
        }

        if (beforeShas.Count == 0)
        {
            return null;
        }

        var beforeChecks = await GetChecksAsync(owner, repo, beforeShas, ct);
        if (beforeChecks.Count == 0)
        {
            return null;
        }

        var afterChecks = await GetChecksAsync(owner, repo, afterShas, ct);

        // A check still red on the commit that actually merged is one the team shipped with, so a
        // pass -> fail flip on it is a known failure they accepted, not a regression this fix is answerable for.
        var mergedHeadFailed = await MergedHeadFailedAsync(owner, repo, pr.Head?.Sha, afterShas, afterChecks, ct);

        var result = BuildResult(
            pr.Number, pr.Title, CopilotFixTrigger.CopilotMention, afterShas,
            analysisPresent: comments.Any(c => Mentions(c, AnalysisMarker)),
            beforeChecks, afterChecks, mergedHeadFailed);

        if (result?.PipelineOutcome != CopilotPipelineOutcome.CopilotPipelineFixSuccess)
        {
            return result;
        }

        // Copilot's fix is only worth counting if it was still there at the merge, so anything a human landed
        // on top of it has to be weighed against it. Merge commits are left out: their diff against the first
        // parent is everything the branch pulled in from main.
        var survivingLandings = landedOnTop
            .Where(commit => !IsMergeCommit(commit) && !IsCopilotAuthored(commit) && !AuthoredBy(commit, ActionsBotName))
            .ToList();
        (result.Verification, result.JudgeVerdict) = await survivalJudge.EvaluateAsync(
            owner, repo, pr.Number, afterShas, survivingLandings, model, ct);

        return result;
    }

    // Handles the auto-fix workflow case. Copilot put its fix on a separate pull request of its own, whose
    // branch name carries both the pull request it is fixing and the commit whose checks failed. So the
    // comparison is that failing commit against the head of this fix pull request.
    private async Task<CopilotPipelineFixResult?> CollectWorkflowFixAsync(
        string owner,
        string repo,
        PullRequest fixPr,
        IReadOnlyDictionary<int, PullRequest> originals,
        CancellationToken ct)
    {
        var branchMatch = FixBranch.Match(fixPr.Head?.Ref ?? string.Empty);
        var fixHeadSha = fixPr.Head?.Sha;
        if (!branchMatch.Success || string.IsNullOrEmpty(fixHeadSha))
        {
            return null;
        }

        var originalPrNumber = int.Parse(branchMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var failingSha = branchMatch.Groups[2].Value;

        if (!originals.TryGetValue(originalPrNumber, out var originalPr))
        {
            return null;
        }

        var beforeChecks = await GetChecksAsync(owner, repo, [failingSha], ct);
        if (beforeChecks.Count == 0)
        {
            return null;
        }

        var afterChecks = await WorkflowAfterChecksAsync(owner, repo, fixPr.Number, fixHeadSha, ct);
        var mergedHeadFailed = await MergedHeadFailedAsync(owner, repo, originalPr.Head?.Sha, [fixHeadSha], afterChecks, ct);

        var result = BuildResult(
            originalPrNumber, originalPr.Title, CopilotFixTrigger.GitHubActionsWorkflow, [fixHeadSha],
            // The workflow only opens a fix pull request after analysing the failure.
            analysisPresent: true,
            beforeChecks, afterChecks, mergedHeadFailed,
            fixPrNumber: fixPr.Number);

        if (result?.PipelineOutcome == CopilotPipelineOutcome.CopilotPipelineFixSuccess)
        {
            // Graded on adoption, not survival: it already fixed the pipeline on its own fix pull request, so
            // the only question left is whether that fix reached the original.
            result.Verification = fixPr.MergedAt != null
                ? CopilotFixVerification.CopilotVerifiedFix
                : CopilotFixVerification.CopilotFixNotMerged;
        }

        return result;
    }

    // A check only says something about the fix if it ran on both commits and changed result. When no check
    // did, there is no row at all, which is different from the fix succeeding or failing.
    private static CopilotPipelineFixResult? BuildResult(
        int prNumber,
        string? prTitle,
        CopilotFixTrigger trigger,
        IReadOnlyList<string> copilotShas,
        bool analysisPresent,
        CheckSet beforeChecks,
        CheckSet afterChecks,
        IReadOnlySet<string> mergedHeadFailed,
        int? fixPrNumber = null)
    {
        var fixedChecks = Sorted(beforeChecks.Failed.Intersect(afterChecks.Passed));
        var brokenChecks = Sorted(beforeChecks.Passed.Intersect(afterChecks.Failed)
            .Where(name => !mergedHeadFailed.Contains(name)));
        if (fixedChecks.Count == 0 && brokenChecks.Count == 0)
        {
            return null;
        }

        return new CopilotPipelineFixResult
        {
            PrNumber = prNumber,
            PrTitle = prTitle,
            FixPrNumber = fixPrNumber,
            Trigger = trigger,
            CopilotCommitShas = copilotShas.ToList(),
            ChecksFixed = fixedChecks,
            ChecksBroken = brokenChecks,
            PipelineOutcome = brokenChecks.Count > 0
                ? CopilotPipelineOutcome.CopilotPipelineFixFailure
                : CopilotPipelineOutcome.CopilotPipelineFixSuccess,
            // The caller replaces this only when the pipeline recovered; otherwise there is no fix to verify.
            Verification = CopilotFixVerification.NotApplicable,
            AnalysisCommentPresent = analysisPresent,
        };
    }

    // The check names still failing on the commit that merged.
    private async Task<IReadOnlySet<string>> MergedHeadFailedAsync(
        string owner,
        string repo,
        string? mergedHeadSha,
        IReadOnlyList<string> afterShas,
        CheckSet afterChecks,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(mergedHeadSha))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (afterShas.Count == 1 && afterShas[0].Equals(mergedHeadSha, StringComparison.OrdinalIgnoreCase))
        {
            return afterChecks.Failed;
        }

        return (await GetChecksAsync(owner, repo, [mergedHeadSha], ct)).Failed;
    }

    private async Task<CheckSet> GetChecksAsync(string owner, string repo, IReadOnlyList<string> shas, CancellationToken ct)
    {
        List<(string, string?)> checks = [];
        foreach (var sha in shas)
        {
            ct.ThrowIfCancellationRequested();
            checks.AddRange((await gitHubService.GetCommitCheckRunsAsync(owner, repo, sha, ct))
                .Select(c => (c.Name, c.Conclusion)));
        }

        return ToCheckSet(checks);
    }

    // A retargeted fix pull request re-runs CI against its new base, so the live check runs no longer reflect
    // the result that gated the fix. Prefer the bot's own record of the runs for this exact head where it exists.
    private async Task<CheckSet> WorkflowAfterChecksAsync(
        string owner,
        string repo,
        int fixPrNumber,
        string fixHeadSha,
        CancellationToken ct)
    {
        var comments = await gitHubService.GetPullRequestIssueCommentsAsync(owner, repo, fixPrNumber, ct);

        foreach (var comment in comments)
        {
            var body = comment.Body;
            if (body == null || !body.Contains(EvidenceMarker, StringComparison.Ordinal))
            {
                continue;
            }

            var recordedSha = EvidenceCommit.Match(body).Groups[1].Value;
            if (!recordedSha.Equals(fixHeadSha, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var recorded = ToCheckSet(ParseEvidenceTable(body));
            if (recorded.Count > 0)
            {
                return recorded;
            }
        }

        return await GetChecksAsync(owner, repo, [fixHeadSha], ct);
    }

    // Rows of the markdown table the bot posts, as "<status_emoji> CONCLUSION | [check name](url) | ...".
    private static IEnumerable<(string Name, string? Conclusion)> ParseEvidenceTable(string commentBody)
    {
        foreach (var line in commentBody.Split('\n'))
        {
            var cells = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length < 2)
            {
                continue;
            }

            var name = EvidenceCheckName.Match(cells[1]);
            yield return (
                name.Success ? name.Groups[1].Value : cells[1],
                cells[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault());
        }
    }

    // Later shas win, so a check re-run across the shas of one side settles on its most recent conclusion.
    private static CheckSet ToCheckSet(IEnumerable<(string Name, string? Conclusion)> checks)
    {
        CheckSet set = new(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

        foreach (var (name, conclusion) in checks)
        {
            if (conclusion == null)
            {
                continue;
            }

            if (Failed.Contains(conclusion))
            {
                set.Passed.Remove(name);
                set.Failed.Add(name);
            }
            else if (conclusion.Equals(Passed, StringComparison.OrdinalIgnoreCase))
            {
                set.Failed.Remove(name);
                set.Passed.Add(name);
            }
        }

        return set;
    }

    /// <summary>
    /// Finds the commits of this pull request that came after the given ones. A commit came after them if its
    /// parent did, so the search starts from the given commits and keeps adding children of what it has found.
    /// Parents outside the pull request are ignored, so the search never leaves this branch.
    /// </summary>
    private static List<PullRequestCommit> CommitsAfter(
        IReadOnlyList<string> startShas,
        IReadOnlyList<PullRequestCommit> commits)
    {
        // Everything the search has reached so far, which is the commits it started from plus the ones it found.
        var reached = new HashSet<string>(startShas, StringComparer.OrdinalIgnoreCase);
        List<PullRequestCommit> commitsAfter = [];

        // A single pass only finds a commit whose parent was already reached, and the commits are not
        // guaranteed to arrive in that order, so keep passing over them until a pass finds nothing new.
        bool foundMore;
        do
        {
            foundMore = false;
            foreach (var commit in commits)
            {
                if (reached.Contains(commit.Sha) || commit.Parents == null)
                {
                    continue;
                }

                if (!commit.Parents.Any(parent => reached.Contains(parent.Sha)))
                {
                    continue;
                }

                reached.Add(commit.Sha);
                commitsAfter.Add(commit);
                foundMore = true;
            }
        }
        while (foundMore);

        return commitsAfter;
    }

    private static bool IsCopilotAuthored(PullRequestCommit commit)
    {
        return CopilotAuthors.Any(name => AuthoredBy(commit, name))
            && string.Equals(commit.Commit?.Committer?.Name, GitHubCommitterName, StringComparison.OrdinalIgnoreCase)
            && commit.Commit?.Verification?.Verified == true;
    }

    private static bool IsMergeCommit(PullRequestCommit commit) => (commit.Parents?.Count ?? 0) > 1;

    private static bool AuthoredBy(PullRequestCommit commit, string name) =>
        string.Equals(commit.Author?.Login, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(commit.Commit?.Author?.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(IssueComment comment, string text) =>
        comment.Body?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsBotComment(IssueComment comment) =>
        comment.User?.Login?.EndsWith(BotLoginSuffix, StringComparison.OrdinalIgnoreCase) != false;

    private static List<string> Sorted(IEnumerable<string> names) =>
        names.Order(StringComparer.OrdinalIgnoreCase).ToList();
}
