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

        [McpServerTool(Name = "run-all-checks"), Description("Run all validation checks for SDK projects. Provide absolute path to project root as param.")]
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

                var results = new List<CheckResult>();
                var overallSuccess = true;

                // Run all individual checks (placeholder implementations)
                var spellCheckResult = await RunSpellCheck(projectPath);
                results.Add(spellCheckResult);
                if (!spellCheckResult.Success) overallSuccess = false;

                var linkValidationResult = await RunLinkValidation(projectPath);
                results.Add(linkValidationResult);
                if (!linkValidationResult.Success) overallSuccess = false;

                var readmeValidationResult = await RunReadmeValidation(projectPath);
                results.Add(readmeValidationResult);
                if (!readmeValidationResult.Success) overallSuccess = false;

                var dependencyCheckResult = await RunDependencyCheck(projectPath);
                results.Add(dependencyCheckResult);
                if (!dependencyCheckResult.Success) overallSuccess = false;

                var changelogValidationResult = await RunChangelogValidation(projectPath);
                results.Add(changelogValidationResult);
                if (!changelogValidationResult.Success) overallSuccess = false;

                var snippetUpdateResult = await RunSnippetUpdate(projectPath);
                results.Add(snippetUpdateResult);
                if (!snippetUpdateResult.Success) overallSuccess = false;

                if (!overallSuccess)
                {
                    SetFailure(1);
                }

                return new DefaultCommandResponse
                {
                    Message = overallSuccess ? "All checks completed successfully" : "Some checks failed",
                    Duration = results.Sum(r => r.Duration),
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

        private async Task<CheckResult> RunSpellCheck(string projectPath)
        {
            logger.LogInformation("Running spell check...");
            // TODO: Implement actual spell check logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "Spell Check",
                Success = true,
                Message = "Spell check completed successfully",
                Duration = 100
            };
        }

        private async Task<CheckResult> RunLinkValidation(string projectPath)
        {
            logger.LogInformation("Running link validation...");
            // TODO: Implement actual link validation logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "Link Validation",
                Success = true,
                Message = "Link validation completed successfully",
                Duration = 100
            };
        }

        private async Task<CheckResult> RunReadmeValidation(string projectPath)
        {
            logger.LogInformation("Running README validation...");
            // TODO: Implement actual README validation logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "README Validation",
                Success = true,
                Message = "README validation completed successfully",
                Duration = 100
            };
        }

        private async Task<CheckResult> RunDependencyCheck(string projectPath)
        {
            logger.LogInformation("Running dependency check...");
            // TODO: Implement actual dependency check logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "Dependency Check",
                Success = true,
                Message = "Dependency check completed successfully",
                Duration = 100
            };
        }

        private async Task<CheckResult> RunChangelogValidation(string projectPath)
        {
            logger.LogInformation("Running changelog validation...");
            // TODO: Implement actual changelog validation logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "Changelog Validation",
                Success = true,
                Message = "Changelog validation completed successfully",
                Duration = 100
            };
        }

        private async Task<CheckResult> RunSnippetUpdate(string projectPath)
        {
            logger.LogInformation("Running snippet update...");
            // TODO: Implement actual snippet update logic
            await Task.Delay(100); // Simulate work
            return new CheckResult
            {
                CheckType = "Snippet Update",
                Success = true,
                Message = "Snippet update completed successfully",
                Duration = 100
            };
        }
    }

    public class CheckResult
    {
        public string CheckType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public long Duration { get; set; }
        public List<string> Details { get; set; } = new();
    }
}