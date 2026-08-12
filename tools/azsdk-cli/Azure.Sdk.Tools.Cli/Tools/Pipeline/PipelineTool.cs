// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[Description("This type contains the MCP tool to get pipeline status.")]
[McpServerToolType]
public class PipelineTool(
    IPipelineIdentifierHelper pipelineHelper,
    ILogger<PipelineTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string getPipelineStatusCommandName = "status";
    private const string GetPipelineStatusToolName = "azsdk_get_pipeline_status";

    private readonly Option<string> projectOpt = new("--project", "-p")
    {
        Description = "Pipeline project name",
        Required = false,
    };

    protected override Command GetCommand() =>
        new McpCommand(getPipelineStatusCommandName, "Get pipeline run status", GetPipelineStatusToolName) { SharedOptions.PipelineLocator, projectOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(SharedOptions.PipelineLocator);
        var project = parseResult.GetValue(projectOpt);

        return await GetPipelineRunStatus(pipelineIdentifier, project, ct);
    }

    [McpServerTool(Name = GetPipelineStatusToolName), Description("Get pipeline status for a given Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")]
    public async Task<ObjectCommandResponse> GetPipelineRunStatus(
        [Description("Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")] string pipelineIdentifier,
        [Description("Pipeline project name (optional)")] string? project = null,
        CancellationToken ct = default
    ) {
        try
        {
            var builds = await pipelineHelper.ResolveBuildsAsync(pipelineIdentifier, project, ct);

            if (builds.Count == 0)
            {
                return new ObjectCommandResponse
                {
                    ResponseError = $"No failed Azure Pipeline builds found for {pipelineIdentifier}"
                };
            }

            return new ObjectCommandResponse
            {
                Result = builds
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pipeline run for {pipelineIdentifier}", pipelineIdentifier);
            return new ObjectCommandResponse
            {
                ResponseError = $"Failed to get pipeline run for {pipelineIdentifier}. Error: {ex.Message}"
            };
        }
    }
}
