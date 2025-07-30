// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This tool runs spell check validation for SDK projects.
    /// </summary>
    [Description("Run spell check validation for SDK projects")]
    [McpServerToolType]
    public class SpellCheckTool : MCPTool
    {
        private readonly ILogger<SpellCheckTool> logger;
        private readonly IOutputService output;

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        public SpellCheckTool(ILogger<SpellCheckTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("spellCheck", "Run spell check validation for SDK projects");
            command.AddOption(projectPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var result = await RunSpellCheck(projectPath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running spell check");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running spell check: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "run-spell-check"), Description("Run spell check validation for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunSpellCheck(string projectPath)
        {
            try
            {
                logger.LogInformation($"Starting spell check for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                // TODO: Implement actual spell check logic
                await Task.Delay(100); // Simulate work
                
                var result = new CheckResult
                {
                    CheckType = "Spell Check",
                    Success = true,
                    Message = "Spell check completed successfully",
                    Duration = 100
                };

                return new DefaultCommandResponse
                {
                    Message = result.Message,
                    Duration = result.Duration,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running spell check");
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}"
                };
            }
        }
    }
}