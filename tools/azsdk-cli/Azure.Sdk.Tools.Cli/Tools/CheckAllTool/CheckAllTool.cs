// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
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
        private readonly IGitHelper gitHelper;

        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };

        public CheckAllTool(
            ILogger<CheckAllTool> logger, 
            IOutputService output,
            IGitHelper gitHelper) : base()
        {
            this.logger = logger;
            this.output = output;
            this.gitHelper = gitHelper;
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

                // Convert ICLICheckResponse to DefaultCommandResponse for output
                var response = new DefaultCommandResponse
                {
                    Message = result.ExitCode == 0 ? "All checks completed successfully" : "Some checks failed",
                    Duration = 0,
                    Result = result
                };

                output.Output(response);
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
        public async Task<ICLICheckResponse> RunAllChecks(string projectPath)
        {
            try
            {
                logger.LogInformation($"Starting all checks for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Project path does not exist: {projectPath}");
                }

                var results = new List<ICLICheckResponse>();
                var overallSuccess = true;

                // Create DependencyCheckTool instance for dependency checking
                var dependencyCheckTool = new DependencyCheckTool(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<DependencyCheckTool>.Instance,
                    output);
                
                var dependencyCheckResult = await dependencyCheckTool.RunDependencyCheck(projectPath);
                
                results.Add(dependencyCheckResult);
                if (dependencyCheckResult.ExitCode != 0) overallSuccess = false;

                var changelogValidationResult = await RunChangelogValidation(projectPath);
                results.Add(changelogValidationResult);
                if (changelogValidationResult.ExitCode != 0) overallSuccess = false;


                if (!overallSuccess)
                {
                    SetFailure(1);
                }

                var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
                var combinedOutput = string.Join("\n", results.Select(r => r.Output));
                
                return overallSuccess 
                    ? new SuccessCLICheckResponse(0, combinedOutput) 
                    : new FailureCLICheckResponse(1, combinedOutput, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running checks");
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }

    private async Task<ICLICheckResponse> RunChangelogValidation(string projectPath)
    {
        logger.LogInformation("Running changelog validation...");
        
        // Use the actual ChangelogValidationTool instead of stub implementation
        var changelogValidationTool = new ChangelogValidationTool(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ChangelogValidationTool>.Instance,
            output,
            gitHelper);
        
        return await changelogValidationTool.RunChangelogValidation(projectPath);
    }

    }
}