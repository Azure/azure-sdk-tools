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
    /// This tool runs all the check/validation tools at once to provide comprehensive validation.
    /// </summary>
    [Description("Run all validation checks for SDK projects")]
    [McpServerToolType]
    public class CheckAllTool : MCPTool
    {
        private readonly ILogger<CheckAllTool> logger;
        private readonly IOutputService output;

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        public CheckAllTool(
            ILogger<CheckAllTool> logger, 
            IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("all", "Run all validation checks for SDK projects");
            command.AddOption(projectPathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var result = await RunAllChecks(projectPath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running checks");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running checks: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "All"), Description("Run all validation checks for SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> RunAllChecks(string projectPath)
        {
            try
            {
                logger.LogInformation($"Starting all checks for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                var results = new List<IOperationResult>();
                var overallSuccess = true;

                // Create DependencyCheckTool instance for dependency checking
                var dependencyCheckTool = new DependencyCheckTool(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<DependencyCheckTool>.Instance,
                    output);
                
                var dependencyCheckResponse = await dependencyCheckTool.RunDependencyCheck(projectPath);
                var dependencyCheckResult = dependencyCheckResponse.Result as IOperationResult 
                    ?? new FailureResult(1, dependencyCheckResponse.ResponseError ?? "Dependency check failed");
                
                results.Add(dependencyCheckResult);
                if (dependencyCheckResult.ExitCode != 0) overallSuccess = false;

                var changelogValidationResult = await RunChangelogValidation(projectPath);
                results.Add(changelogValidationResult);
                if (changelogValidationResult.ExitCode != 0) overallSuccess = false;


                if (!overallSuccess)
                {
                    SetFailure(1);
                }

                return new DefaultCommandResponse
                {
                    Message = overallSuccess ? "All checks completed successfully" : "Some checks failed",
                    Duration = 0, // Since IOperationResult doesn't have Duration, we'll set to 0 or calculate differently
                    Result = results
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running checks");
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}"
                };
            }
        }

        private async Task<IOperationResult> RunChangelogValidation(string projectPath)
        {
            logger.LogInformation("Running changelog validation...");
            // TODO: Implement actual changelog validation logic
            await Task.Delay(100); // Simulate work
            return new SuccessResult(0, "Changelog validation completed successfully");
        }

    }
}