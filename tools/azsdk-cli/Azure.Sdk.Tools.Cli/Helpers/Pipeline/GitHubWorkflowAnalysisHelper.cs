// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
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
    public async Task<List<GitHubWorkflowRunAnalysis>> AnalyzeWorkflowsAsync(BuildGitHubSource source, CancellationToken ct)
    {
        var workflowRuns = await gitHubService.GetFailedWorkflowRunsForCommitAsync(source.Owner, source.Repo, source.HeadSha!, ct);

        List<GitHubWorkflowRunAnalysis> runAnalyses = [];
        foreach (var run in workflowRuns)
        {
            runAnalyses.Add(await AnalyzeWorkflowRunAsync(source, run, ct));
        }

        return runAnalyses;
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
    /// Collects the logs and jobs for a single workflow run. Each read is attempted independently so a run
    /// whose logs have expired (or whose jobs cannot be listed) is still reported with whatever was available.
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

        try
        {
            var logs = await gitHubService.GetWorkflowRunLogsAsync(source.Owner, source.Repo, run.Id, ct);
            if (!string.IsNullOrEmpty(logs))
            {
                using var reader = new StringReader(logs);
                runAnalysis.Logs = await logAnalysisHelper.AnalyzeLogContent(reader, null, null, null, url: run.HtmlUrl, ct: ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read logs for workflow run {runId}", run.Id);
            (runAnalysis.Errors ??= []).Add($"Failed to read workflow run logs: {ex.Message}");
        }

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

        return runAnalysis;
    }
}
