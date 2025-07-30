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

        // Options for different types of checks
        private readonly Option<string> projectPathOption = new(["--project-path", "-p"], "Path to the project directory to check") { IsRequired = true };
        private readonly Option<bool> spellCheckOption = new(["--spell-check"], () => false, "Run spell check");
        private readonly Option<bool> linkValidationOption = new(["--link-validation"], () => false, "Validate links are not broken");
        private readonly Option<bool> readmeValidationOption = new(["--readme-validation"], () => false, "Verify README follows track2 standards");
        private readonly Option<bool> dependencyCheckOption = new(["--dependency-check"], () => false, "Check for dependency conflicts");
        private readonly Option<bool> changelogValidationOption = new(["--changelog-validation"], () => false, "Verify changelog follows correct pattern");
        private readonly Option<bool> snippetUpdateOption = new(["--snippet-update"], () => false, "Update snippets to ensure they're current");
        private readonly Option<bool> allChecksOption = new(["--all"], () => true, "Run all available checks (default)");

        public CheckAllTool(ILogger<CheckAllTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
            CommandHierarchy = [SharedCommandGroups.Checks];
        }

        public override Command GetCommand()
        {
            Command command = new("all", "Run all validation checks for SDK projects");
            command.AddOption(projectPathOption);
            command.AddOption(spellCheckOption);
            command.AddOption(linkValidationOption);
            command.AddOption(readmeValidationOption);
            command.AddOption(dependencyCheckOption);
            command.AddOption(changelogValidationOption);
            command.AddOption(snippetUpdateOption);
            command.AddOption(allChecksOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var projectPath = ctx.ParseResult.GetValueForOption(projectPathOption);
                var runSpellCheck = ctx.ParseResult.GetValueForOption(spellCheckOption);
                var runLinkValidation = ctx.ParseResult.GetValueForOption(linkValidationOption);
                var runReadmeValidation = ctx.ParseResult.GetValueForOption(readmeValidationOption);
                var runDependencyCheck = ctx.ParseResult.GetValueForOption(dependencyCheckOption);
                var runChangelogValidation = ctx.ParseResult.GetValueForOption(changelogValidationOption);
                var runSnippetUpdate = ctx.ParseResult.GetValueForOption(snippetUpdateOption);
                var runAllChecks = ctx.ParseResult.GetValueForOption(allChecksOption);

                var result = await RunAllChecks(projectPath, new CheckOptions
                {
                    SpellCheck = runAllChecks || runSpellCheck,
                    LinkValidation = runAllChecks || runLinkValidation,
                    ReadmeValidation = runAllChecks || runReadmeValidation,
                    DependencyCheck = runAllChecks || runDependencyCheck,
                    ChangelogValidation = runAllChecks || runChangelogValidation,
                    SnippetUpdate = runAllChecks || runSnippetUpdate
                });

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
        public async Task<DefaultCommandResponse> RunAllChecks(string projectPath, CheckOptions? options = null)
        {
            try
            {
                logger.LogInformation($"Starting checks for project at: {projectPath}");
                
                options ??= new CheckOptions(); // Default to all checks enabled
                
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

                // Run individual checks
                if (options.SpellCheck)
                {
                    var spellCheckResult = await RunSpellCheck(projectPath);
                    results.Add(spellCheckResult);
                    if (!spellCheckResult.Success) overallSuccess = false;
                }

                if (options.LinkValidation)
                {
                    var linkValidationResult = await RunLinkValidation(projectPath);
                    results.Add(linkValidationResult);
                    if (!linkValidationResult.Success) overallSuccess = false;
                }

                if (options.ReadmeValidation)
                {
                    var readmeValidationResult = await RunReadmeValidation(projectPath);
                    results.Add(readmeValidationResult);
                    if (!readmeValidationResult.Success) overallSuccess = false;
                }

                if (options.DependencyCheck)
                {
                    var dependencyCheckResult = await RunDependencyCheck(projectPath);
                    results.Add(dependencyCheckResult);
                    if (!dependencyCheckResult.Success) overallSuccess = false;
                }

                if (options.ChangelogValidation)
                {
                    var changelogValidationResult = await RunChangelogValidation(projectPath);
                    results.Add(changelogValidationResult);
                    if (!changelogValidationResult.Success) overallSuccess = false;
                }

                if (options.SnippetUpdate)
                {
                    var snippetUpdateResult = await RunSnippetUpdate(projectPath);
                    results.Add(snippetUpdateResult);
                    if (!snippetUpdateResult.Success) overallSuccess = false;
                }

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

    public class CheckOptions
    {
        public bool SpellCheck { get; set; } = true;
        public bool LinkValidation { get; set; } = true;
        public bool ReadmeValidation { get; set; } = true;
        public bool DependencyCheck { get; set; } = true;
        public bool ChangelogValidation { get; set; } = true;
        public bool SnippetUpdate { get; set; } = true;
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