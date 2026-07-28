// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers.Pipeline;

public interface IGitHubWorkflowAnalysisHelper
{
    /// <summary>
    /// Collects the failed workflow runs for the source's commit, with the logs and jobs of each. Successful
    /// runs are skipped: their logs carry no failure to explain and would otherwise dominate the analysis.
    /// </summary>
    Task<List<GitHubWorkflowRunAnalysis>> AnalyzeWorkflowsAsync(BuildGitHubSource source, CancellationToken ct);

    /// <summary>
    /// Lists the checks reported as failing on the source's pull request. This covers the red checks that no
    /// workflow run accounts for: results published by Azure Pipelines, and commit statuses posted by
    /// aggregating workflows that succeed while reporting a failure. Returns an empty list when the build was
    /// not triggered by a pull request.
    /// </summary>
    Task<List<PrCheckRun>> GetFailingChecksAsync(BuildGitHubSource source, CancellationToken ct);
}

public class GitHubWorkflowAnalysisHelper(
    IGitHubService gitHubService,
    ILogAnalysisHelper logAnalysisHelper,
    ILogger<GitHubWorkflowAnalysisHelper> logger
) : IGitHubWorkflowAnalysisHelper
{
    // The GitHub Actions runner marks the failure that ends a step with this prefix.
    private static readonly List<string> RunnerErrorAnnotations = ["##[error]"];

    public async Task<List<GitHubWorkflowRunAnalysis>> AnalyzeWorkflowsAsync(BuildGitHubSource source, CancellationToken ct)
    {
        var workflowRuns = await gitHubService.GetFailedWorkflowRunsForCommitAsync(source.Owner, source.Repo, source.HeadSha!, ct);

        List<GitHubWorkflowRunAnalysis> runAnalyses = [];
        foreach (var run in MostRecentRunPerWorkflow(workflowRuns))
        {
            runAnalyses.Add(await AnalyzeWorkflowRunAsync(source, run, ct));
        }

        return runAnalyses;
    }

    private IEnumerable<WorkflowRun> MostRecentRunPerWorkflow(IReadOnlyList<WorkflowRun> runs)
    {
        var latest = runs
            .GroupBy(run => run.WorkflowId)
            .Select(group => group.OrderByDescending(run => run.CreatedAt).First())
            .ToList();

        if (latest.Count < runs.Count)
        {
            logger.LogDebug("Skipped {count} failed workflow run(s) superseded by a later run of the same workflow", runs.Count - latest.Count);
        }

        return latest;
    }

    public async Task<List<PrCheckRun>> GetFailingChecksAsync(BuildGitHubSource source, CancellationToken ct)
    {
        if (source.PullRequestNumber == null)
        {
            return [];
        }

        var checks = await gitHubService.GetPrCheckRunsAsync(source.Owner, source.Repo, source.PullRequestNumber.Value, ct);
        return checks.Where(check => check.IsFailed).ToList();
    }

    /// <summary>
    /// Collects what explains a single workflow run's failure: the logs of the steps that failed, or, when
    /// the run published none, the job list so the failure is at least named. Each read is attempted
    /// independently so a run whose logs have expired is still reported with whatever was available.
    /// </summary>
    private async Task<GitHubWorkflowRunAnalysis> AnalyzeWorkflowRunAsync(BuildGitHubSource source, WorkflowRun run, CancellationToken ct)
    {
        var runAnalysis = new GitHubWorkflowRunAnalysis
        {
            Name = run.Name,
            Status = run.Status.StringValue,
            Conclusion = run.Conclusion?.StringValue,
            Url = run.HtmlUrl,
        };

        string? logs = null;
        try
        {
            logs = await gitHubService.GetFailedWorkflowRunLogsAsync(source.Owner, source.Repo, run.Id, ct);
            if (!string.IsNullOrEmpty(logs))
            {
                runAnalysis.Logs = await AnalyzeLogsAsync(logs, run.HtmlUrl, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read logs for workflow run {runId}", run.Id);
            (runAnalysis.Errors ??= []).Add($"Failed to read workflow run logs: {ex.Message}");
        }

        if (string.IsNullOrEmpty(logs))
        {
            try
            {
                var jobs = await gitHubService.GetWorkflowRunJobsAsync(source.Owner, source.Repo, run.Id, ct);
                runAnalysis.Jobs.AddRange(jobs.Select(job => $"{job.Name}: {job.Conclusion?.StringValue ?? job.Status.StringValue}"));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read jobs for workflow run {runId}", run.Id);
                (runAnalysis.Errors ??= []).Add($"Failed to read workflow run jobs: {ex.Message}");
            }
        }

        return runAnalysis;
    }

    /// <summary>
    /// Extracts the failure from a run's logs, keyed on the annotation the runner writes for it. The general
    /// keywords anchor on any line containing "error", which picks up benign "ERROR:" notices from tools that
    /// went on to succeed, so they are only used as a fallback when no annotation is present.
    /// </summary>
    private async Task<List<LogEntry>> AnalyzeLogsAsync(string logs, string url, CancellationToken ct)
    {
        using (var annotatedReader = new StringReader(logs))
        {
            var annotated = await logAnalysisHelper.AnalyzeLogContent(annotatedReader, RunnerErrorAnnotations, null, null, url: url, ct: ct);
            if (annotated?.Count > 0)
            {
                return annotated;
            }
        }

        logger.LogDebug("No runner error annotation found in the logs for {url}; falling back to the general error keywords", url);
        using var reader = new StringReader(logs);
        return await logAnalysisHelper.AnalyzeLogContent(reader, null, null, null, url: url, ct: ct);
    }
}
