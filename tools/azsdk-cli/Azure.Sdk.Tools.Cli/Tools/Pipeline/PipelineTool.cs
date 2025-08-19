// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline
{
    [Description("This type contains the MCP tool to get pipeline status.")]
    [McpServerToolType]
    public class PipelineTool(IDevOpsService devopsService,
        IOutputHelper output,
        ILogger<PipelineTool> logger) : MCPTool
    {

        // Commands
        private const string getPipelineStatusCommandName = "get-status";

        // Options
        private readonly Option<int> pipelineRunIdOpt = new(["--pipeline-id"], "pipeline run id") { IsRequired = true };


        /// <summary>
        /// Get pipeline run details and status for a given pipeline build ID
        /// </summary>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <returns></returns>
        [McpServerTool(Name = "azsdk_get_pipeline_status"), Description("Get pipeline status for a given pipeline build ID")]
        public async Task<string> GetPipelineRunStatus(int buildId)
        {
            try
            {
                var response = new GenericResponse();
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
                if (pipeline != null)
                {
                    response.Status = pipeline.Result?.ToString() ?? pipeline.Status?.ToString() ?? "Not available";
                    response.Details.Add($"Pipeline run link: {DevOpsService.GetPipelineUrl(pipeline.Id)}");
                }
                return output.Format(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse
                {
                    Status = "Failed"
                };
                logger.LogError(ex, "Failed to get pipeline run with id {buildId}", buildId);
                errorResponse.Details.Add($"Failed to get pipeline run with id {buildId}. Error: {ex.Message}");
                return output.Format(errorResponse);
            }
        }
        

        public override Command GetCommand()
        {
            var command = new Command("pipeline", "Commands to help with DevOps pipeline");
            var subCommands = new[]
            {
                new Command(getPipelineStatusCommandName, "Get pipeline run status") { pipelineRunIdOpt }
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;
            switch (command)
            {
                case getPipelineStatusCommandName:
                    var pipelineRunStatus = await GetPipelineRunStatus(commandParser.GetValueForOption(pipelineRunIdOpt));
                    output.Output($"Pipeline run status: {pipelineRunStatus}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }
    }
}
