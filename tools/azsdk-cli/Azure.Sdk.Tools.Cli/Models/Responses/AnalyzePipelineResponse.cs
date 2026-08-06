using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class AnalyzePipelineResponse : CommandResponse
{
    [JsonPropertyName("azure_pipeline_analyses")]
    public List<AzurePipelineAnalysis> AzurePipelineAnalyses { get; set; } = [];

    /// <summary>
    /// Failed GitHub Actions runs for the commit the builds were queued against. Independent of the pipeline
    /// analyses: a red workflow run does not mean a pipeline failed, and vice versa. Null when the pipeline's
    /// source is not a GitHub repository or no commit could be resolved.
    /// </summary>
    [JsonPropertyName("github_workflow_analyses")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GitHubWorkflowRunAnalysis>? GitHubWorkflowAnalyses { get; set; }

    /// <summary>
    /// Every check GitHub reports as failing on the pull request, so the analysis can be reconciled against
    /// what the PR page shows. Included without logs because a failing check is not itself a source of
    /// diagnostics: it is either a view of a build or workflow run detailed above, or a status posted by an
    /// aggregating workflow that restates another failure. Null when no pull request could be resolved.
    /// </summary>
    [JsonPropertyName("failing_pull_request_checks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PrCheckRun>? FailingPullRequestChecks { get; set; }

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
    };

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (AzurePipelineAnalyses.Count == 0)
        {
            sb.AppendLine("No failed Azure Pipeline builds found.");
        }

        foreach (var pipelineAnalysis in AzurePipelineAnalyses)
        {
            sb.AppendLine($"Build: {pipelineAnalysis.PipelineBuild.BuildId} Project: {pipelineAnalysis.PipelineBuild.Project} PipelineUrl: {pipelineAnalysis.PipelineBuild.PipelineUrl}");

            if (pipelineAnalysis.PipelineBuild.IsInProgress)
            {
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine($"Azure Pipeline not complete (status: {pipelineAnalysis.PipelineBuild.Status})");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("The azure pipeline has not finished running, so failure logs and test result artifacts");
                sb.AppendLine("may not be published yet. Any results below are partial, re-run the analysis");
                sb.AppendLine("once the pipeline completes for the full picture.");
                sb.AppendLine();
            }

            if (pipelineAnalysis.FailedPipelineTests is { Count: > 0 } failedTests)
            {
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("Failed Tests");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine(JsonSerializer.Serialize(failedTests, jsonOptions));
            }

            if (pipelineAnalysis.FailedPipelineTasks is { HasErrors: true } failedTasks)
            {
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("Failed Tasks");
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine(failedTasks.ToString());
                sb.AppendLine("--------------------------------------------------------------------------------");
            }

            if (pipelineAnalysis.Errors?.Count > 0)
            {
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("Analysis could not be completed for this build:");
                sb.AppendLine("--------------------------------------------------------------------------------");
                foreach (var error in pipelineAnalysis.Errors)
                {
                    sb.AppendLine($"- {error}");
                }
                sb.AppendLine("--------------------------------------------------------------------------------");
            }

            if ((pipelineAnalysis.FailedPipelineTests?.Count ?? 0) == 0 && pipelineAnalysis.FailedPipelineTasks?.HasErrors != true && (pipelineAnalysis.Errors?.Count ?? 0) == 0)
            {
                sb.AppendLine("");
                sb.AppendLine(pipelineAnalysis.PipelineBuild.IsInProgress
                    ? "No failures found yet, the azure pipeline is still running."
                    : "No failures found");
            }
        }

        AppendGitHubWorkflowAnalysis(sb);
        AppendFailingChecks(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Reports the GitHub Actions runs in their own section. These are deliberately not folded into any build's
    /// output: they run on the same commit but are otherwise unrelated to the pipeline, and attributing an
    /// Actions failure to a build would be misleading.
    /// </summary>
    private void AppendGitHubWorkflowAnalysis(StringBuilder sb)
    {
        if (GitHubWorkflowAnalyses == null || GitHubWorkflowAnalyses.Count == 0)
        {
            return;
        }

        foreach (var runAnalysis in GitHubWorkflowAnalyses)
        {
            sb.AppendLine($"Workflow: {runAnalysis.Name} Status: {runAnalysis.Status} Conclusion: {runAnalysis.Conclusion} Url: {runAnalysis.Url}");

            foreach (var job in runAnalysis.Jobs)
            {
                sb.AppendLine($"  Job: {job}");
            }

            foreach (var logEntry in runAnalysis.Logs)
            {
                sb.AppendLine($"  Log line {logEntry.Line}:");
                sb.AppendLine(logEntry.Message);
            }

            foreach (var error in runAnalysis.Errors ?? [])
            {
                sb.AppendLine($"  - Analysis incomplete: {error}");
            }
        }
    }

    /// <summary>
    /// Lists the failing checks last, as a reconciliation against the pull request page rather than as new
    /// findings. The list is expected to overlap the sections above; an entry that appears only here is a
    /// check whose failure originates somewhere this analysis cannot reach.
    /// </summary>
    private void AppendFailingChecks(StringBuilder sb)
    {
        if (FailingPullRequestChecks == null || FailingPullRequestChecks.Count == 0)
        {
            return;
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Failing checks on the pull request");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var check in FailingPullRequestChecks)
        {
            sb.AppendLine($"  {check.Name} [{check.Conclusion}] Reported by: {check.AppName} Url: {check.DetailsUrl}");
        }
    }
}
