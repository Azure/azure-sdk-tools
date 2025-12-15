// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline
{
    [Description("This type contains the MCP tool to get pipeline status.")]
    [McpServerToolType]
    public class PipelineTool(IDevOpsService devopsService, ILogger<PipelineTool> logger) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

        // Commands
        private const string getPipelineStatusCommandName = "status";
        private const string GetPipelineStatusToolName = "azsdk_get_pipeline_status";

        // Options
        private readonly Option<int> pipelineRunIdOpt = new("--pipeline-id")
        {
            Description = "pipeline run id",
            Required = true,
        };

        protected override Command GetCommand() =>
            new McpCommand(getPipelineStatusCommandName, "Get pipeline run status", GetPipelineStatusToolName) { pipelineRunIdOpt };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;
            switch (command)
            {
                case getPipelineStatusCommandName:
                    var pipelineRunStatus = await GetPipelineRunStatus(parseResult.GetValue(pipelineRunIdOpt));
                    return new DefaultCommandResponse { Message = $"Pipeline run status: {pipelineRunStatus}" };
                default:
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        /// <summary>
        /// Get pipeline run details and status for a given pipeline build ID
        /// </summary>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <returns></returns>
        [McpServerTool(Name = GetPipelineStatusToolName), Description("Get pipeline status for a given pipeline build ID")]
        public async Task<DefaultCommandResponse> GetPipelineRunStatus(int buildId)
        {
            try
            {
                var response = new DefaultCommandResponse();
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
                if (pipeline != null)
                {
                    response.Result = pipeline.Result?.ToString() ?? pipeline.Status?.ToString() ?? "Not available";
                    response.Message = $"Pipeline run link: {DevOpsService.GetPipelineUrl(pipeline.Id)}";
                }
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get pipeline run with id {buildId}", buildId);
                return new()
                {
                    ResponseError = $"Failed to get pipeline run with id {buildId}. Error: {ex.Message}"
                };
            }
        }
    }
}
