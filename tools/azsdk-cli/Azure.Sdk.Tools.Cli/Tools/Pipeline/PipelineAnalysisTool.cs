// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Net;
using System.Text.Json;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches data from an Azure Pipelines run.")]
public class PipelineAnalysisTool(
    IPipelineAnalysisHelper pipelineAnalysisHelper,
    IGitHubWorkflowAnalysisHelper workflowAnalysisHelper,
    IPipelineIdentifierHelper pipelineIdentifierHelper,
    ICopilotAgentRunner copilotAgentRunner,
    ILogger<PipelineAnalysisTool> logger
) : MCPTool
{
    private readonly Option<int> logIdOpt = new("--log-id")
    {
        Description = "ID of the pipeline task log",
        Required = false,
    };

    private readonly Option<string> projectOpt = new("--project", "-p")
    {
        Description = "Pipeline project name",
        Required = false,
    };

    private readonly Option<bool> copilotOpt = new("--copilot")
    {
        Description = "Use Copilot agent to analyze pipeline failures",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string AnalyzePipelineToolName = "azsdk_analyze_pipeline";

    protected override Command GetCommand() =>
        new McpCommand("analyze", "Analyze a pipeline run", AnalyzePipelineToolName)
        {
            SharedOptions.PipelineLocator, projectOpt, logIdOpt, copilotOpt,
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(SharedOptions.PipelineLocator);
        var project = parseResult.GetValue(projectOpt);
        var logId = parseResult.GetValue(logIdOpt);
        var useCopilot = parseResult.GetValue(copilotOpt);

        var result = await AnalyzePipeline(pipelineIdentifier, project, logId != 0 ? logId : null, ct);

        if (!useCopilot)
        {
            return result;
        }

        return await AnalyzeWithCopilotAsync(result, pipelineIdentifier, ct);
    }

    [McpServerTool(Name = AnalyzePipelineToolName), Description("Analyzes and returns structured failure data and logs from an Azure Pipeline build. Accepts an Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number.")]
    public async Task<AnalyzePipelineResponse> AnalyzePipeline(
        [Description("Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")] string pipelineIdentifier,
        [Description("Pipeline project name (optional)")] string? project = null,
        [Description("Specific log ID to analyze (optional)")] int? logId = null,
        CancellationToken ct = default)
    {
        try
        {
            AnalyzePipelineResponse response = new AnalyzePipelineResponse();

            var builds = await pipelineIdentifierHelper.ResolveBuildsAsync(pipelineIdentifier, project, ct);

            // A pull request can be red on GitHub Actions alone, so when no build backs the identifier the
            // source is resolved from the pull request itself rather than reporting nothing to analyze.
            var commitRef = builds.Count > 0
                ? await pipelineIdentifierHelper.ResolveCommitRefFromBuildsAsync(builds, ct)
                : await pipelineIdentifierHelper.ResolveCommitRefFromPrAsync(pipelineIdentifier, ct);

            if (builds.Count == 0 && commitRef == null)
            {
                response.ResponseError = $"No failed Azure Pipeline builds found for {pipelineIdentifier}";
                return response;
            }

            logger.LogInformation("Analyzing pipeline {pipelineIdentifier}...", pipelineIdentifier);

            if (builds.Count > 0)
            {
                var (pipelineAnalyses, warnings) = await pipelineAnalysisHelper.AnalyzePipelineAsync(builds, logId, ct);
                response.AzurePipelineAnalyses = pipelineAnalyses;
                if (warnings.Count > 0)
                {
                    (response.NextSteps ??= []).AddRange(warnings);
                }
            }

            if (commitRef != null)
            {
                try
                {
                    response.GitHubWorkflowAnalyses = await workflowAnalysisHelper.AnalyzeWorkflowsAsync(commitRef, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to analyze GitHub workflow runs for {owner}/{repo} @ {sha}", commitRef.Owner, commitRef.Repo, commitRef.HeadSha);
                    (response.NextSteps ??= []).Add(
                        $"GitHub Actions runs for {commitRef.Owner}/{commitRef.Repo} @ {commitRef.HeadSha} could not be listed " +
                        $"({ex.Message}); the pipeline results are unaffected.");
                }
            }

            if (commitRef?.PullRequestNumber != null)
            {
                try
                {
                    response.FailingPullRequestChecks = await workflowAnalysisHelper.GetFailingChecksAsync(commitRef, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to list failing checks for {owner}/{repo}#{pr}", commitRef.Owner, commitRef.Repo, commitRef.PullRequestNumber);
                    (response.NextSteps ??= []).Add(
                        $"Failing checks for {commitRef.Owner}/{commitRef.Repo}#{commitRef.PullRequestNumber} could not be listed " +
                        $"({ex.Message}); the results above may not cover every red check on the pull request.");
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze pipeline {pipelineIdentifier}", pipelineIdentifier);
            return new AnalyzePipelineResponse()
            {
                ResponseError = $"Failed to analyze pipeline {pipelineIdentifier}: {ex.Message}",
                NextSteps = NextStepsForFailure(ex),
            };
        }
    }

    /// <summary>
    /// Picks next steps that match how the analysis failed. Public builds and public pull requests are read
    /// without credentials, so telling the user to sign in is misleading for the failures signing in would not
    /// fix: a malformed identifier, or a run or pull request that does not exist. Failures that already carry
    /// their own instructions - the DevOps and GitHub CLI sign-in errors - are left to speak for themselves.
    /// </summary>
    private static List<string>? NextStepsForFailure(Exception ex) => ex switch
    {
        ArgumentException =>
        [
            "Pass an Azure Pipelines run link, a build ID, a GitHub pull request link, or a pull request number.",
        ],
        BuildNotFoundException =>
        [
            "Check that the identifier names a run that still exists, and that any project in its link is correct.",
        ],
        VssUnauthorizedException or VssServiceResponseException { HttpStatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
        [
            "Make sure you're authenticated with the Azure CLI (`az login --tenant microsoft.onmicrosoft.com`) and have access to the azure-sdk DevOps project (https://aka.ms/azsdk/access).",
        ],
        Octokit.NotFoundException =>
        [
            "Check that the repository and pull request named by the identifier exist.",
        ],
        Octokit.ApiException =>
        [
            "If the repository is private, or the request was rate limited, authenticate the GitHub CLI (`gh auth login`) and try again.",
        ],
        _ => null,
    };

    /// <summary>
    /// Summarizes a completed pipeline analysis with the Copilot agent as a markdown root-cause report.
    /// </summary>
    private async Task<CommandResponse> AnalyzeWithCopilotAsync(AnalyzePipelineResponse pipelineResult, string pipelineIdentifier, CancellationToken ct)
    {
        try
        {
            // Serialize as JSON to ensure Copilot gets the full context (FailedPipelineTasks, FailedPipelineTests)
            // even when ResponseErrors is set, since ToString() suppresses Format() output on errors.
            var pipelineData = JsonSerializer.Serialize(pipelineResult, new JsonSerializerOptions { WriteIndented = true });

            var tempPath = Path.Combine(Path.GetTempPath(), $"pipeline-analysis-{Guid.NewGuid():N}.md");
            await File.WriteAllTextAsync(tempPath, pipelineData, ct);
            logger.LogInformation("Pipeline analysis data written to {tempPath}", tempPath);
            logger.LogInformation("Run `copilot -i 'Fix the pipeline failures detailed in {tempPath}'` to attempt a fix", tempPath);

            var instructions = $"""
                You are a pipeline failure analyst. You have been given the output of a CI/CD pipeline analysis.
                Your job is to examine the `failed_pipeline_tasks` and `failed_pipeline_tests` of each entry in
                `azure_pipeline_analyses`, identify root causes, and provide a clear, actionable summary for a
                developer.

                Respond in markdown format. Structure your response as:
                1. **Root Cause Analysis** - What likely caused each failure
                2. **Summary** - A brief overview of what failed
                3. **Recommended Actions** - Concrete steps the developer should take to fix the issues

                If there are no failures, state that the pipeline appears healthy.

                If any build in `azure_pipeline_analyses` has a `pipeline_build.status` that is present and is
                neither "completed" nor "Not available", that build is still running: note that its failure
                logs and test artifacts may not be published yet, so the analysis may be incomplete
                and the developer should re-run it once the build finishes. In that case do not
                describe that build as healthy just because no failures were found.

                `github_workflow_analyses`, if present, holds GitHub Actions runs for the same commit, with
                their failures in `logs` and `jobs` rather than the `failed_pipeline_*` fields above.
                These are independent of the pipeline builds: report them separately and do not treat an
                Actions failure as a pipeline failure (or vice versa).

                `failing_pull_request_checks`, if present, lists every check GitHub reports as red on the pull
                request. This can be a duplicate of a pipeline or Actions failure. But it is a catch all for
                anything that failed.

                Here is the pipeline analysis data:

                {pipelineData}
                """;

            var agent = new CopilotAgent<string>
            {
                Instructions = instructions,
                MaxIterations = 3,
            };

            var analysis = await copilotAgentRunner.RunAsync(agent, ct);

            return new DefaultCommandResponse { Message = analysis };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run Copilot analysis for pipeline {pipelineIdentifier}", pipelineIdentifier);
            return new DefaultCommandResponse
            {
                ResponseError = $"Failed to run Copilot analysis: {ex.Message}"
            };
        }
    }
}
