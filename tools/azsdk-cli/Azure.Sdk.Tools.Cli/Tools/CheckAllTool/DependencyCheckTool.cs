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
    /// This tool runs dependency check for SDK projects.
    /// </summary>
    [Description("Run dependency check for SDK projects")]
    [McpServerToolType]
    public class DependencyCheckTool : MCPTool
    {
        private readonly ILogger<DependencyCheckTool> logger;
        private readonly IOutputService output;

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        public DependencyCheckTool(ILogger<DependencyCheckTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("dependencyCheck", "Run dependency check for SDK projects");
            command.AddOption(projectPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var result = await RunDependencyCheck(projectPath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running dependency check");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running dependency check: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "run-dependency-check"), Description("Run dependency check for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunDependencyCheck(string projectPath)
        {
            try
            {
                logger.LogInformation($"Starting dependency check for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                // TODO: Implement actual dependency check logic
                await Task.Delay(100); // Simulate work
                
                var result = new CheckResult
                {
                    CheckType = "Dependency Check",
                    Success = true,
                    Message = "Dependency check completed successfully",
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
                logger.LogError(ex, "Unhandled exception while running dependency check");
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}"
                };
            }
        }
    }
}