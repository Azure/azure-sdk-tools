// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Services;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers.EngSys;

public interface IPipelineFixEvaluatorHelper
{
    Task<List<PipelineFixEvaluation>> EvaluatePipelineFixesAsync(
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
    private const string AnalysisMarker = "[Pilot] PR Pipeline Failure Analysis";
    private const string AutomatedFixMarker = "**Automated fix:**";
    private const string ActionsBotName = "github-actions[bot]";
    private const string Success = "SUCCESS";

    private static readonly Regex FixBranch =
        new(@"^pipeline-fix/pr-(\d+)-([0-9a-f]{40})/", RegexOptions.IgnoreCase);
    private static readonly Regex FixCompareLink = new(
        @"https://github\.com/[^/\s)]+/[^/\s)]+/compare/[^\s)]+?\.\.\.(?<branch>pipeline-fix/pr-(?<pr>\d+)-(?<sha>[0-9a-f]{40})/run-(?<run>\d+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DiffHunk = new(
        @"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",
        RegexOptions.Compiled);
    private static readonly HashSet<string> Failed = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAILURE", "ERROR", "TIMED_OUT", "STARTUP_FAILURE",
    };

    private sealed record CheckSet(HashSet<string> Failed, HashSet<string> Passed)
    {
        public int Count => Failed.Count + Passed.Count;
    }

    private sealed record WorkflowOutcome(
        CopilotPipelineOutcome? PipelineOutcome,
        CopilotFixVerification Verification,
        string AdoptedCommitSha);

    public async Task<List<PipelineFixEvaluation>> EvaluatePipelineFixesAsync(
        string owner,
        string repo,
        DateTimeOffset since,
        DateTimeOffset until,
        CancellationToken ct)
    {
        var mergedPullRequests = await gitHubService.GetMergedPullRequestsByTimeFrameAsync(owner, repo, since, until, ct);
        List<PipelineFixEvaluation> results = [];

        foreach (var pullRequest in mergedPullRequests
            .Where(pr => pr.MergedAt != null && !FixBranch.IsMatch(pr.Head?.Ref ?? string.Empty))
            .OrderBy(pr => pr.Number))
        {
            var result = new PipelineFixEvaluation
            {
                PrNumber = pullRequest.Number,
                PrTitle = pullRequest.Title,
                Verification = CopilotFixVerification.NotApplicable,
            };
            results.Add(result);

            try
            {
                var comments = await gitHubService.GetPullRequestIssueCommentsAsync(owner, repo, pullRequest.Number, ct);
                if (!comments.Any(IsWorkflowRequested))
                {
                    continue;
                }

                var links = WorkflowFixLinks(pullRequest, comments);
                if (links.Count == 0)
                {
                    continue;
                }

                result.FixWorkflowRun = WorkflowRunId(links[0]);
                result.FixBranchOpened = FixBranchName(links[0]);
                result.Verification = CopilotFixVerification.CopilotFixNotMerged;
                var commits = await GetListableCommitsAsync(owner, repo, pullRequest.Number, ct);
                if (commits == null)
                {
                    continue;
                }

                foreach (var link in links)
                {
                    var outcome = await EvaluateWorkflowFixAsync(owner, repo, pullRequest, commits, link, ct);
                    if (outcome == null)
                    {
                        continue;
                    }

                    result.FixWorkflowRun = WorkflowRunId(link);
                    result.FixBranchOpened = FixBranchName(link);
                    result.FixPullRequestMerged = outcome.AdoptedCommitSha;
                    result.PipelineOutcome = outcome.PipelineOutcome;
                    result.Verification = outcome.Verification;
                    if (outcome.PipelineOutcome == CopilotPipelineOutcome.CopilotPipelineFixSuccess)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not evaluate workflow progression for pull request {PrNumber}", pullRequest.Number);
            }
        }

        return results;
    }

    private async Task<WorkflowOutcome?> EvaluateWorkflowFixAsync(
        string owner,
        string repo,
        PullRequest pullRequest,
        IReadOnlyList<PullRequestCommit> commits,
        Match link,
        CancellationToken ct)
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
            logger.LogWarning("The workflow fix branch {FixBranch} for pull request {PrNumber} no longer exists", fixBranch, pullRequest.Number);
            return null;
        }

        var adoptedCommit = commits.FirstOrDefault(commit =>
            commit.Sha.Equals(fixHeadSha, StringComparison.OrdinalIgnoreCase));
        adoptedCommit ??= await FindEquivalentPatchAsync(
            owner, repo, fixHeadSha, CommitsAfter(failingSha, commits), ct);
        if (adoptedCommit == null)
        {
            return null;
        }

        var beforeChecks = await GetChecksAsync(owner, repo, [failingSha], ct);
        var afterChecks = await GetChecksAsync(owner, repo, [adoptedCommit.Sha], ct);
        if (beforeChecks.Count == 0)
        {
            return new(null, CopilotFixVerification.NotApplicable, adoptedCommit.Sha);
        }

        var fixedChecks = beforeChecks.Failed.Intersect(afterChecks.Passed).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var brokenChecks = beforeChecks.Passed.Intersect(afterChecks.Failed).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (fixedChecks.Count == 0 || brokenChecks.Count > 0)
        {
            return new(
                CopilotPipelineOutcome.CopilotPipelineFixFailure,
                CopilotFixVerification.NotApplicable,
                adoptedCommit.Sha);
        }

        var laterHumanCommits = CommitsAfter(adoptedCommit.Sha, commits).Where(IsHumanCommit).ToList();
        var verification = await HasHumanOverlapAsync(owner, repo, adoptedCommit.Sha, laterHumanCommits, ct)
            ? CopilotFixVerification.CopilotFixOverridden
            : CopilotFixVerification.CopilotVerifiedFix;
        return new(CopilotPipelineOutcome.CopilotPipelineFixSuccess, verification, adoptedCommit.Sha);
    }

    private static List<Match> WorkflowFixLinks(PullRequest pullRequest, IReadOnlyList<IssueComment> comments) =>
        comments
            .Where(comment => AuthoredBy(comment, ActionsBotName) && Mentions(comment, AnalysisMarker))
            .SelectMany(comment => FixCompareLink.Matches(comment.Body ?? string.Empty).Cast<Match>())
            .Where(match => int.Parse(match.Groups["pr"].Value, CultureInfo.InvariantCulture) == pullRequest.Number)
            .DistinctBy(match => match.Groups["branch"].Value)
            .ToList();

    private static long WorkflowRunId(Match link) =>
        long.Parse(link.Groups["run"].Value, CultureInfo.InvariantCulture);

    private static string FixBranchName(Match link) =>
        Uri.UnescapeDataString(link.Groups["branch"].Value);

    private static bool IsWorkflowRequested(IssueComment comment) =>
        AuthoredBy(comment, ActionsBotName)
        && Mentions(comment, AnalysisMarker)
        && Mentions(comment, AutomatedFixMarker);

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

        logger.LogWarning("Skipping pull request {PrNumber}: GitHub returned at least {CommitListLimit} commits", prNumber, CommitListLimit);
        return null;
    }

    private async Task<CheckSet> GetChecksAsync(
        string owner,
        string repo,
        IReadOnlyList<string> shas,
        CancellationToken ct)
    {
        CheckSet checks = new(new(StringComparer.OrdinalIgnoreCase), new(StringComparer.OrdinalIgnoreCase));
        foreach (var sha in shas)
        {
            foreach (var check in await gitHubService.GetCommitCheckRunsAsync(owner, repo, sha, ct))
            {
                if (Failed.Contains(check.Conclusion ?? string.Empty))
                {
                    checks.Passed.Remove(check.Name);
                    checks.Failed.Add(check.Name);
                }
                else if (Success.Equals(check.Conclusion, StringComparison.OrdinalIgnoreCase))
                {
                    checks.Failed.Remove(check.Name);
                    checks.Passed.Add(check.Name);
                }
            }
        }
        return checks;
    }

    private async Task<PullRequestCommit?> FindEquivalentPatchAsync(
        string owner,
        string repo,
        string fixCommitSha,
        IReadOnlyList<PullRequestCommit> laterCommits,
        CancellationToken ct)
    {
        var fixFiles = await gitHubService.GetCommitFilesAsync(owner, repo, fixCommitSha, ct);
        var comparableFixFiles = fixFiles
            .Where(file => PatchChanges(file.Patch) != null)
            .ToList();
        if (comparableFixFiles.Count == 0)
        {
            return null;
        }

        foreach (var commit in laterCommits)
        {
            var files = await gitHubService.GetCommitFilesAsync(owner, repo, commit.Sha, ct);
            if (comparableFixFiles.Any(fixFile => files.Any(file => SameChanges(fixFile, file))))
            {
                return commit;
            }
        }
        return null;
    }

    private async Task<bool> HasHumanOverlapAsync(
        string owner,
        string repo,
        string adoptedCommitSha,
        IReadOnlyList<PullRequestCommit> humanCommits,
        CancellationToken ct)
    {
        if (humanCommits.Count == 0)
        {
            return false;
        }

        var fixFiles = await gitHubService.GetCommitFilesAsync(owner, repo, adoptedCommitSha, ct);
        List<IReadOnlyList<GitHubCommitFile>> humanFilesByCommit = [];
        foreach (var commit in humanCommits)
        {
            humanFilesByCommit.Add(await gitHubService.GetCommitFilesAsync(owner, repo, commit.Sha, ct));
        }

        foreach (var fixFile in fixFiles)
        {
            var matchingHumanCommits = humanFilesByCommit
                .Select(files => files.Where(file => FileNamesOverlap(fixFile, file)).ToList())
                .Where(files => files.Count > 0)
                .ToList();
            foreach (var humanFile in matchingHumanCommits.SelectMany(files => files))
            {
                var fixLines = ChangedLines(fixFile.Patch, useNewSide: true);
                var humanLines = ChangedLines(humanFile.Patch, useNewSide: false);
                if (fixLines == null || humanLines == null || fixLines.Overlaps(humanLines))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IReadOnlyList<PullRequestCommit> CommitsAfter(
        string failingSha,
        IReadOnlyList<PullRequestCommit> commits)
    {
        var reached = new HashSet<string>([failingSha], StringComparer.OrdinalIgnoreCase);
        List<PullRequestCommit> later = [];
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
                later.Add(commit);
                foundMore = true;
            }
        }
        while (foundMore);
        return later;
    }

    private static bool IsHumanCommit(PullRequestCommit commit)
    {
        var author = commit.Author?.Login ?? commit.Commit?.Author?.Name;
        return (commit.Parents?.Count ?? 0) <= 1
            && !string.IsNullOrWhiteSpace(author)
            && !author.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameChanges(GitHubCommitFile left, GitHubCommitFile right)
    {
        var leftChanges = PatchChanges(left.Patch);
        var rightChanges = PatchChanges(right.Patch);
        return FileNamesOverlap(left, right)
            && leftChanges != null
            && rightChanges != null
            && leftChanges.Value.Added.SequenceEqual(rightChanges.Value.Added)
            && leftChanges.Value.Removed.SequenceEqual(rightChanges.Value.Removed);
    }

    private static (IReadOnlyList<string> Added, IReadOnlyList<string> Removed)? PatchChanges(string? patch)
    {
        if (string.IsNullOrEmpty(patch))
        {
            return null;
        }

        var added = patch.Split('\n')
            .Where(line => line.StartsWith('+'))
            .Select(line => line[1..])
            .Order(StringComparer.Ordinal)
            .ToList();
        var removed = patch.Split('\n')
            .Where(line => line.StartsWith('-'))
            .Select(line => line[1..])
            .Order(StringComparer.Ordinal)
            .ToList();
        return added.Count == 0 && removed.Count == 0 ? null : (added, removed);
    }

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
                changed.UnionWith(useNewSide
                    ? [newLine]
                    : [Math.Max(1, oldLine - 1), Math.Max(1, oldLine)]);
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

    private static bool AuthoredBy(IssueComment comment, string name) =>
        string.Equals(comment.User?.Login, name, StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(IssueComment comment, string text) =>
        comment.Body?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;
}
