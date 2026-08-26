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
/// comments @copilot, or its auto-fix workflow publishes a separate branch linked from the analysis comment.
/// Both report the same thing - the commit from before the fix and the commit from after it - so the checks
/// that went from failing to passing, and the ones that went the other way, read off the difference.
/// </summary>
public interface IPipelineFixEvaluatorHelper
{
    Task<List<CopilotPipelineFixResult>> EvaluatePipelineFixesAsync(
        string owner,
        string repo,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken ct);
}

public class PipelineFixEvaluatorHelper(
    IGitHubService gitHubService,
    ILogger<PipelineFixEvaluatorHelper> logger
) : IPipelineFixEvaluatorHelper
{
    private const int CommitListLimit = 250;
    private const string CopilotMention = "@copilot";
    private const string AnalysisMarker = "[Pilot] PR Pipeline Failure Analysis";
    private const string BotLoginSuffix = "[bot]";
    private const string GitHubCommitterName = "GitHub";
    private const string ActionsBotName = "github-actions[bot]";
    private static readonly string[] CopilotAuthors = ["Copilot", "copilot-swe-agent[bot]"];
    private static readonly Regex FixBranch =
        new(@"^pipeline-fix/pr-(\d+)-([0-9a-f]{40})/", RegexOptions.IgnoreCase);
    private static readonly Regex FixCompareLink = new(
        @"https://github\.com/[^/\s)]+/[^/\s)]+/compare/[^\s)]+?\.\.\.(?<branch>pipeline-fix/pr-(?<pr>\d+)-(?<sha>[0-9a-f]{40})/run-\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DiffHunk = new(
        @"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",
        RegexOptions.Compiled);

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
        CancellationToken ct)
    {
        List<CopilotPipelineFixResult> results = [];

        var mergedPrs = await gitHubService.GetMergedPullRequestsByTimeFrameAsync(owner, repo, since, until, ct);
        foreach (var pr in mergedPrs)
        {
            // A pull request that never merged has no accepted outcome to measure, and one whose own head is
            // a pipeline-fix/ branch is a fix already counted against the pull request it was written for.
            if (pr.MergedAt == null || FixBranch.IsMatch(pr.Head?.Ref ?? string.Empty))
            {
                continue;
            }

            try
            {
                // Both routes read this same thread: the mention route for the human @copilot comment, the
                // workflow route for the compare link its analysis comment carries.
                var comments = await gitHubService.GetPullRequestIssueCommentsAsync(owner, repo, pr.Number, ct);

                var mentionResult = await CollectMentionFixAsync(owner, repo, pr, comments, ct);
                if (mentionResult != null)
                {
                    results.Add(mentionResult);
                }

                results.AddRange(await CollectWorkflowFixesAsync(owner, repo, pr, comments, ct));
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

        return results.OrderBy(r => r.PrNumber).ThenBy(r => r.FixBranch ?? string.Empty).ToList();
    }

    private async Task<CopilotPipelineFixResult?> CollectMentionFixAsync(
        string owner,
        string repo,
        PullRequest pr,
        IReadOnlyList<IssueComment> comments,
        CancellationToken ct)
    {
        // A person has to have written the @copilot comment, because Copilot ignores a mention from another
        // bot. The failure-analysis comment is recorded but not required, so a hand-written mention counts too.
        if (!comments.Any(c => Mentions(c, CopilotMention) && !IsBotComment(c)))
        {
            return null;
        }

        var commits = await GetListableCommitsAsync(owner, repo, pr.Number, ct);
        var copilotCommits = commits?.Where(IsCopilotAuthored).ToList() ?? [];
        if (copilotCommits.Count == 0)
        {
            return null;
        }

        var afterShas = copilotCommits.Select(c => c.Sha).ToList();
        var landedOnTop = CommitsAfter(afterShas, commits!);
        var fixOrLater = new HashSet<string>(
            afterShas.Concat(landedOnTop.Select(c => c.Sha)), StringComparer.OrdinalIgnoreCase);

        var beforeShas = copilotCommits
            .Select(commit => commit.Parents?.FirstOrDefault()?.Sha)
            .OfType<string>()
            .Where(sha => sha.Length > 0 && !fixOrLater.Contains(sha))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (beforeShas.Count == 0)
        {
            return null;
        }

        return await EvaluateFixAsync(
            owner, repo, pr, CopilotFixTrigger.CopilotMention, beforeShas, afterShas,
            analysisPresent: comments.Any(c => Mentions(c, AnalysisMarker)),
            // A fix delivered this way is taken over when a person afterwards rewrites the lines Copilot
            // changed, because the pipeline that merged then reflects their edit and not Copilot's.
            isOverriddenAsync: (_, _) => HasHumanOverlapAsync(owner, repo, afterShas, Humans(landedOnTop), ct),
            fixBranch: null, ct);
    }

    // The auto-fix workflow route: the workflow publishes its fix on a branch of its own and replaces
    // "Automated fix: Requested" in the pull request's analysis comment with a compare link to it. The branch
    // name carries the pull request the fix is for and the commit that was failing when it ran.
    private async Task<List<CopilotPipelineFixResult>> CollectWorkflowFixesAsync(
        string owner,
        string repo,
        PullRequest pr,
        IReadOnlyList<IssueComment> comments,
        CancellationToken ct)
    {
        List<CopilotPipelineFixResult> results = [];
        var links = comments
            .Where(comment => AuthoredBy(comment, ActionsBotName) && Mentions(comment, AnalysisMarker))
            .SelectMany(comment => FixCompareLink.Matches(comment.Body ?? string.Empty).Cast<Match>())
            .Where(match => int.Parse(match.Groups["pr"].Value, CultureInfo.InvariantCulture) == pr.Number)
            .DistinctBy(match => match.Groups["branch"].Value)
            .ToList();

        if (links.Count == 0)
        {
            return results;
        }

        var commits = await GetListableCommitsAsync(owner, repo, pr.Number, ct);
        if (commits == null)
        {
            return results;
        }

        foreach (var link in links)
        {
            var fixBranch = Uri.UnescapeDataString(link.Groups["branch"].Value);
            var failingSha = link.Groups["sha"].Value;
            string fixHeadSha;
            try
            {
                fixHeadSha = await gitHubService.GetBranchHeadShaAsync(owner, repo, fixBranch, ct);
            }
            catch (NotFoundException)
            {
                logger.LogWarning(
                    "Skipping workflow fix branch {FixBranch} for pull request {PrNumber}: the branch no longer exists and its commit was not recorded.",
                    fixBranch, pr.Number);
                continue;
            }

            var adopted = commits.Any(commit => commit.Sha.Equals(fixHeadSha, StringComparison.OrdinalIgnoreCase))
                || await HasEquivalentPatchAsync(
                    owner, repo, fixHeadSha, Humans(CommitsAfter([failingSha], commits)), ct);

            if (!adopted)
            {
                results.Add(new CopilotPipelineFixResult
                {
                    PrNumber = pr.Number,
                    PrTitle = pr.Title,
                    FixBranch = fixBranch,
                    Trigger = CopilotFixTrigger.GitHubActionsWorkflow,
                    CopilotCommitShas = [fixHeadSha],
                    Verification = CopilotFixVerification.CopilotFixNotMerged,
                    AnalysisCommentPresent = true,
                });
                continue;
            }

            var result = await EvaluateFixAsync(
                owner, repo, pr, CopilotFixTrigger.GitHubActionsWorkflow, [failingSha], [fixHeadSha],
                analysisPresent: true,
                isOverriddenAsync: (fixedChecks, mergedHeadFailed) =>
                    Task.FromResult(fixedChecks.Any(mergedHeadFailed.Contains)),
                fixBranch, ct);

            if (result != null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<CopilotPipelineFixResult?> EvaluateFixAsync(
        string owner,
        string repo,
        PullRequest pr,
        CopilotFixTrigger trigger,
        IReadOnlyList<string> beforeShas,
        IReadOnlyList<string> afterShas,
        bool analysisPresent,
        Func<List<string>, IReadOnlySet<string>, Task<bool>> isOverriddenAsync,
        string? fixBranch,
        CancellationToken ct)
    {
        var beforeChecks = await GetChecksAsync(owner, repo, beforeShas, ct);
        if (beforeChecks.Count == 0)
        {
            return null;
        }

        var afterChecks = await GetChecksAsync(owner, repo, afterShas, ct);

        // A check still red on the commit that actually merged is one the team shipped with, so a
        // pass -> fail flip on it is a known failure they accepted.
        HashSet<string> mergedHeadFailed;
        if (string.IsNullOrEmpty(pr.MergeCommitSha))
        {
            mergedHeadFailed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            mergedHeadFailed = afterShas.Count == 1 && afterShas[0].Equals(pr.MergeCommitSha, StringComparison.OrdinalIgnoreCase)
                ? afterChecks.Failed
                : (await GetChecksAsync(owner, repo, [pr.MergeCommitSha], ct)).Failed;
        }

        var fixedChecks = Sorted(beforeChecks.Failed.Intersect(afterChecks.Passed));
        var brokenChecks = Sorted(beforeChecks.Passed.Intersect(afterChecks.Failed)
            .Where(name => !mergedHeadFailed.Contains(name)));
        if (fixedChecks.Count == 0 && brokenChecks.Count == 0)
        {
            return null;
        }

        return new CopilotPipelineFixResult
        {
            PrNumber = pr.Number,
            PrTitle = pr.Title,
            FixBranch = fixBranch,
            Trigger = trigger,
            CopilotCommitShas = afterShas.ToList(),
            ChecksFixed = fixedChecks,
            ChecksBroken = brokenChecks,
            PipelineOutcome = brokenChecks.Count > 0
                ? CopilotPipelineOutcome.CopilotPipelineFixFailure
                : CopilotPipelineOutcome.CopilotPipelineFixSuccess,
            Verification = brokenChecks.Count > 0
                ? CopilotFixVerification.NotApplicable
                : await isOverriddenAsync(fixedChecks, mergedHeadFailed)
                    ? CopilotFixVerification.CopilotFixOverridden
                    : CopilotFixVerification.CopilotVerifiedFix,
            AnalysisCommentPresent = analysisPresent,
        };
    }

    private async Task<IReadOnlyList<PullRequestCommit>?> GetListableCommitsAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken ct)
    {
        var commits = await gitHubService.GetPullRequestCommitsAsync(owner, repo, prNumber, ct);
        if (commits.Count < CommitListLimit)
        {
            return commits;
        }

        logger.LogWarning(
            "Skipping pull request {PrNumber}: it has at least {CommitListLimit} commits, which is as many as GitHub will list.",
            prNumber, CommitListLimit);
        return null;
    }

    // Later shas win, so a check re-run across the shas of one side settles on its most recent conclusion.
    private async Task<CheckSet> GetChecksAsync(string owner, string repo, IReadOnlyList<string> shas, CancellationToken ct)
    {
        CheckSet set = new(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));

        foreach (var sha in shas)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var check in await gitHubService.GetCommitCheckRunsAsync(owner, repo, sha, ct))
            {
                // A conclusion that is null, or one of the inconclusive ones, belongs to neither side.
                if (Failed.Contains(check.Conclusion ?? string.Empty))
                {
                    set.Passed.Remove(check.Name);
                    set.Failed.Add(check.Name);
                }
                else if (Passed.Equals(check.Conclusion, StringComparison.OrdinalIgnoreCase))
                {
                    set.Failed.Remove(check.Name);
                    set.Passed.Add(check.Name);
                }
            }
        }

        return set;
    }

    // Whether a person rewrote the lines Copilot's fix changed.
    private async Task<bool> HasHumanOverlapAsync(
        string owner,
        string repo,
        IReadOnlyList<string> copilotCommitShas,
        IReadOnlyList<PullRequestCommit> humanCommits,
        CancellationToken ct)
    {
        if (humanCommits.Count == 0)
        {
            return false;
        }

        var copilotFiles = await GetCommitFilesAsync(owner, repo, copilotCommitShas, ct);
        var humanFiles = await GetCommitFilesAsync(owner, repo, humanCommits.Select(commit => commit.Sha), ct);

        foreach (var copilotFile in copilotFiles)
        {
            foreach (var humanFile in humanFiles.Where(file => FileNamesOverlap(copilotFile, file)))
            {
                var copilotLines = ChangedLines(copilotFile.Patch, useNewSide: true);
                var humanLines = ChangedLines(humanFile.Patch, useNewSide: false);

                // GitHub omits patches for binary and oversized diffs. A shared file without usable line
                // evidence cannot be proven disjoint, so reject it conservatively.
                if (copilotLines == null || humanLines == null || copilotLines.Overlaps(humanLines))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a person landed the workflow's patch themselves. Applying the branch by hand gives the same
    /// change a new sha, so the shas alone would report a fix that was in fact used as never used. A patch
    /// GitHub did not render cannot be compared, so it is not claimed as adopted.
    /// </summary>
    private async Task<bool> HasEquivalentPatchAsync(
        string owner,
        string repo,
        string fixCommitSha,
        IReadOnlyList<PullRequestCommit> humanCommits,
        CancellationToken ct)
    {
        var fixFiles = await GetCommitFilesAsync(owner, repo, [fixCommitSha], ct);
        if (fixFiles.Count == 0 || fixFiles.Any(file => string.IsNullOrEmpty(file.Patch)))
        {
            return false;
        }

        foreach (var commit in humanCommits)
        {
            var humanFiles = await GetCommitFilesAsync(owner, repo, [commit.Sha], ct);
            if (fixFiles.Count == humanFiles.Count
                && fixFiles.All(fixFile => humanFiles.Any(humanFile => SamePatch(fixFile, humanFile))))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<List<GitHubCommitFile>> GetCommitFilesAsync(
        string owner,
        string repo,
        IEnumerable<string> shas,
        CancellationToken ct)
    {
        List<GitHubCommitFile> files = [];
        foreach (var sha in shas)
        {
            ct.ThrowIfCancellationRequested();
            files.AddRange(await gitHubService.GetCommitFilesAsync(owner, repo, sha, ct));
        }

        return files;
    }

    private static bool SamePatch(GitHubCommitFile left, GitHubCommitFile right) =>
        string.Equals(left.Filename, right.Filename, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.PreviousFileName, right.PreviousFileName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Status, right.Status, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Patch, right.Patch, StringComparison.Ordinal);

    // A rename means one file is known by two names, so either name matching is the same file.
    private static bool FileNamesOverlap(GitHubCommitFile left, GitHubCommitFile right) =>
        NamesOf(left).Any(name => NamesOf(right).Contains(name, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> NamesOf(GitHubCommitFile file) =>
        new[] { file.Filename, file.PreviousFileName }.Where(name => !string.IsNullOrEmpty(name))!;

    private static HashSet<int>? ChangedLines(string? patch, bool useNewSide)
    {
        if (string.IsNullOrEmpty(patch))
        {
            return null;
        }

        HashSet<int> changed = [];
        var oldLine = 0;
        var newLine = 0;
        var foundHunk = false;

        foreach (var line in patch.Split('\n'))
        {
            var hunk = DiffHunk.Match(line);
            if (hunk.Success)
            {
                oldLine = int.Parse(hunk.Groups[1].Value, CultureInfo.InvariantCulture);
                newLine = int.Parse(hunk.Groups[3].Value, CultureInfo.InvariantCulture);
                foundHunk = true;
                continue;
            }

            if (!foundHunk || line.StartsWith("\\ No newline", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('+'))
            {
                // On the pre-change side an insertion has no line of its own, so it is charged to the
                // boundary it sits between.
                changed.UnionWith(useNewSide
                    ? [newLine]
                    : new[] { Math.Max(1, oldLine - 1), Math.Max(1, oldLine) });
                newLine++;
            }
            else if (line.StartsWith('-'))
            {
                changed.Add(useNewSide ? Math.Max(1, newLine) : oldLine);
                oldLine++;
            }
            else
            {
                oldLine++;
                newLine++;
            }
        }

        return foundHunk ? changed : null;
    }

    private static List<PullRequestCommit> CommitsAfter(
        IReadOnlyList<string> startShas,
        IReadOnlyList<PullRequestCommit> commits)
    {
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
                if (reached.Contains(commit.Sha)
                    || commit.Parents == null
                    || !commit.Parents.Any(parent => reached.Contains(parent.Sha)))
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

    private static List<PullRequestCommit> Humans(IEnumerable<PullRequestCommit> commits) =>
        commits
            .Where(commit => (commit.Parents?.Count ?? 0) <= 1
                && !IsCopilotAuthored(commit)
                && !AuthoredBy(commit, ActionsBotName))
            .ToList();

    private static bool IsCopilotAuthored(PullRequestCommit commit) =>
        CopilotAuthors.Any(name => AuthoredBy(commit, name))
        && string.Equals(commit.Commit?.Committer?.Name, GitHubCommitterName, StringComparison.OrdinalIgnoreCase)
        && commit.Commit?.Verification?.Verified == true;

    private static bool AuthoredBy(PullRequestCommit commit, string name) =>
        string.Equals(commit.Author?.Login, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(commit.Commit?.Author?.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool AuthoredBy(IssueComment comment, string name) =>
        string.Equals(comment.User?.Login, name, StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(IssueComment comment, string text) =>
        comment.Body?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsBotComment(IssueComment comment) =>
        comment.User?.Login?.EndsWith(BotLoginSuffix, StringComparison.OrdinalIgnoreCase) == true;

    private static List<string> Sorted(IEnumerable<string> names) =>
        names.Order(StringComparer.OrdinalIgnoreCase).ToList();
}
