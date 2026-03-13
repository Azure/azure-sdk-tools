// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches test data from Azure Pipelines")]
public class PipelineTestsTool(
    IPipelineIdentifierHelper pipelineHelper,
    IDevOpsService devopsService,
    ILogger<PipelineTestsTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string GetPipelineLlmArtifactsToolName = "azsdk_get_pipeline_llm_artifacts";

    private readonly Option<string> projectOpt = new("--project", "-p")
    {
        Description = "Pipeline project name",
        Required = false,
    };

    protected override Command GetCommand() =>
        new McpCommand("test-results", "Get test results for a pipeline run", GetPipelineLlmArtifactsToolName) { SharedOptions.PipelineLocator, projectOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(SharedOptions.PipelineLocator);
        var project = parseResult.GetValue(projectOpt);

        return await GetPipelineLlmArtifacts(pipelineIdentifier, project);
    }

    [McpServerTool(Name = GetPipelineLlmArtifactsToolName), Description("Downloads artifacts intended for LLM analysis from a pipeline run. Accepts an Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number.")]
    public async Task<ObjectCommandResponse> GetPipelineLlmArtifacts(
        [Description("Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")] string pipelineIdentifier,
        [Description("Pipeline project name (optional)")] string? project = null)
    {
        try
        {
            var builds = await pipelineHelper.ResolveBuildsAsync(pipelineIdentifier, project);

            if (builds.Count == 0)
            {
                return new ObjectCommandResponse
                {
                    ResponseError = $"No failed Azure Pipeline builds found for {pipelineIdentifier}"
                };
            }

            var allArtifacts = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var build in builds)
            {
                var buildProject = build.Project;
                if (string.IsNullOrEmpty(buildProject))
                {
                    buildProject = await pipelineHelper.GetPipelineProjectAsync(build.BuildId);
                }

                logger.LogInformation("Fetching artifacts for build {buildId} in project {project}", build.BuildId, buildProject);
                var result = await devopsService.GetPipelineLlmArtifacts(buildProject, build.BuildId);
                var buildKey = build.PipelineUrl ?? pipelineHelper.GetPipelineUrl(buildProject, build.BuildId);
                allArtifacts[buildKey] = result;
            }

            // If single build, return flat result for backwards compatibility
            if (allArtifacts.Count == 1)
            {
                return new ObjectCommandResponse { Result = allArtifacts.Values.First() };
            }

            return new ObjectCommandResponse { Result = allArtifacts };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pipeline artifacts for {pipelineIdentifier}", pipelineIdentifier);
            return new ObjectCommandResponse
            {
                ResponseError = $"Failed to get pipeline artifacts for {pipelineIdentifier}: {ex.Message}",
            };
        }
    }
}
