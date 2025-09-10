// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// This tool runs validation checks for SDK packages based on the specified check type.
    /// </summary>
    [Description("Run validation checks for SDK packages")]
    [McpServerToolType]
    public class PackageCheckTool : MCPTool
    {
        private readonly ILogger<PackageCheckTool> logger;
        private readonly IOutputHelper output;
        private readonly ILanguageChecks languageChecks;

        public PackageCheckTool(ILogger<PackageCheckTool> logger, IOutputHelper output, ILanguageChecks languageChecks) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageChecks = languageChecks;
            CommandHierarchy = [SharedCommandGroups.Package];
        }

        public override Command GetCommand()
        {
            var parentCommand = new Command("run-checks", "Run validation checks for SDK packages");

            // Add the package path option to the parent command so it can be used without subcommands
            parentCommand.AddOption(SharedOptions.PackagePath);

            // Set handler for the parent command to default to All checks
            parentCommand.SetHandler(async (InvocationContext ctx) =>
            {
                var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                await HandleCommandWithOptions(packagePath, PackageCheckType.All, ctx.GetCancellationToken());
            });

            // Create sub-commands for each check type
            var checkTypeValues = Enum.GetValues<PackageCheckType>();
            foreach (var checkType in checkTypeValues)
            {
                var subCommand = new Command(checkType.ToString().ToLowerInvariant(), $"Run {checkType} validation check");
                subCommand.AddOption(SharedOptions.PackagePath);

                subCommand.SetHandler(async (InvocationContext ctx) =>
                {
                    var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                    await HandleCommandWithOptions(packagePath, checkType, ctx.GetCancellationToken());
                });

                parentCommand.AddCommand(subCommand);
            }

            return parentCommand;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // Get the command name which corresponds to the check type
            var commandName = ctx.ParseResult.CommandResult.Command.Name;

            // If this is the parent command (run-checks), default to All
            if (commandName == "run-checks")
            {
                var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                await HandleCommandWithOptions(packagePath, PackageCheckType.All, ct);
                return;
            }

            // Parse the command name back to enum for subcommands
            if (Enum.TryParse<PackageCheckType>(commandName, true, out var checkType))
            {
                var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                await HandleCommandWithOptions(packagePath, checkType, ct);
            }
            else
            {
                throw new ArgumentException($"Unknown command: {commandName}");
            }
        }

        private async Task HandleCommandWithOptions(string packagePath, PackageCheckType checkType, CancellationToken ct)
        {
            var result = await RunPackageCheck(packagePath, checkType, ct);
            ExitCode = result.ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "azsdk_package_run_check"), Description("Run validation checks for SDK packages. Provide package path and check type (All, Changelog, Dependency, Readme, Cspell, Snippets).")]
        public async Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Starting {checkType} check for package at: {packagePath}");
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                return checkType switch
                {
                    PackageCheckType.All => await RunAllChecks(packagePath, ct),
                    PackageCheckType.Changelog => await RunChangelogValidation(packagePath, ct),
                    PackageCheckType.Dependency => await RunDependencyCheck(packagePath, ct),
                    PackageCheckType.Readme => await RunReadmeValidation(packagePath, ct),
                    PackageCheckType.Cspell => await RunSpellingValidation(packagePath, ct),
                    PackageCheckType.Snippets => await RunSnippetUpdate(packagePath, ct),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(checkType),
                        checkType,
                        $"Unknown check type. Valid values are: {string.Join(", ", Enum.GetNames(typeof(PackageCheckType)))}")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package check");
                SetFailure(1);
                return new CLICheckResponse(1, ex.ToString(), $"Unhandled exception while running {checkType} check");
            }
        }

        private async Task<CLICheckResponse> RunAllChecks(string packagePath, CancellationToken ct)
        {
            logger.LogInformation("Running all validation checks");

            var results = new List<CLICheckResponse>();
            var overallSuccess = true;
            var failedChecks = new List<string>();

            // Run dependency check
            var dependencyCheckResult = await languageChecks.AnalyzeDependenciesAsync(packagePath, ct);
            results.Add(dependencyCheckResult);
            if (dependencyCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Dependency");
            }

            // Run changelog validation
            var changelogValidationResult = await languageChecks.ValidateChangelogAsync(packagePath, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Changelog");
            }

            // Run README validation
            var readmeValidationResult = await languageChecks.ValidateReadmeAsync(packagePath, ct);
            results.Add(readmeValidationResult);
            if (readmeValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("README");
            }

            // Run spelling check
            var spellingCheckResult = await languageChecks.CheckSpellingAsync(packagePath);
            results.Add(spellingCheckResult);
            if (spellingCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Spelling");
            }

            // Run snippet update
            var snippetUpdateResult = await languageChecks.UpdateSnippetsAsync(packagePath, ct);
            results.Add(snippetUpdateResult);
            if (snippetUpdateResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            if (!overallSuccess)
            {
                SetFailure(1);
            }

            var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
            var combinedOutput = string.Join("\n", results.Select(r => r.CheckStatusDetails));
            
            // Generate comprehensive next steps for all checks
            var nextSteps = new List<string>();
            if (overallSuccess)
            {
                nextSteps.Add("All package validation checks passed! Your package is ready for the next steps in the development process.");
                nextSteps.Add("Consider running package release readiness checks if preparing for release.");
            }
            else
            {
                nextSteps.Add($"The following checks failed: {string.Join(", ", failedChecks)}");
                nextSteps.Add("Address the issues identified above before proceeding with package release.");
                nextSteps.Add("Re-run the package checks after making corrections to verify all issues are resolved.");
                
                // Add specific guidance from individual check failures
                foreach (var result in results.Where(r => r.ExitCode != 0 && r.NextSteps?.Any() == true))
                {
                    nextSteps.AddRange(result.NextSteps);
                }
            }

            return overallSuccess
                ? new CLICheckResponse(0, combinedOutput) { NextSteps = nextSteps }
                : new CLICheckResponse(1, combinedOutput, message) { NextSteps = nextSteps };
        }

        private async Task<CLICheckResponse> RunChangelogValidation(string packagePath, CancellationToken ct)
        {
            logger.LogInformation("Running changelog validation");

            var result = await languageChecks.ValidateChangelogAsync(packagePath, ct);

            if (result.ExitCode != 0)
            {
                SetFailure(1);
                result.NextSteps = new List<string>
                {
                    "Review and update the CHANGELOG.md file to ensure it follows the proper format",
                    "Verify that unreleased changes are properly documented",
                    "Check that version numbers and release dates are correctly formatted",
                    "Refer to the Azure SDK changelog guidelines for proper formatting"
                };
                return new CLICheckResponse(result.ExitCode, result.CheckStatusDetails, "Changelog validation failed") { NextSteps = result.NextSteps };
            }
            else
            {
                result.NextSteps = new List<string>
                {
                    "Changelog validation passed - no action needed"
                };
            }

            return result;
        }

        private async Task<CLICheckResponse> RunDependencyCheck(string packagePath, CancellationToken ct)
        {
            logger.LogInformation("Running dependency check");

            var result = await languageChecks.AnalyzeDependenciesAsync(packagePath, ct);
            
            if (result.ExitCode != 0)
            {
                result.NextSteps = new List<string>
                {
                    "Review and update package dependencies to resolve conflicts",
                    "Ensure all dependencies meet Azure SDK guidelines",
                    "Check for outdated or vulnerable dependencies",
                    "Run language-specific dependency update commands (e.g., pip upgrade, npm update)"
                };
            }
            else
            {
                result.NextSteps = new List<string>
                {
                    "Dependency check passed - all dependencies are properly configured"
                };
            }
            
            return result;
        }

        private async Task<CLICheckResponse> RunReadmeValidation(string packagePath, CancellationToken ct = default)
        {
            logger.LogInformation("Running README validation");

            var result = await languageChecks.ValidateReadmeAsync(packagePath, ct);
            
            if (result.ExitCode != 0)
            {
                result.NextSteps = new List<string>
                {
                    "Create or update the README.md file to include required sections",
                    "Ensure the README follows Azure SDK documentation standards",
                    "Include proper installation instructions, usage examples, and API documentation links",
                    "Verify that all code samples in the README are working and up-to-date"
                };
            }
            else
            {
                result.NextSteps = new List<string>
                {
                    "README validation passed - documentation is properly formatted"
                };
            }
            
            return result;
        }

        private async Task<CLICheckResponse> RunSpellingValidation(string packagePath, CancellationToken ct = default)
        {
            logger.LogInformation("Running spelling validation");

            var result = await languageChecks.CheckSpellingAsync(packagePath, ct);
            
            if (result.ExitCode != 0)
            {
                result.NextSteps = new List<string>
                {
                    "Fix spelling errors identified in the package files",
                    "Add legitimate technical terms to the cspell dictionary if needed",
                    "Review comments, documentation, and variable names for typos",
                    "Run cspell locally to identify and fix spelling issues before committing"
                };
            }
            else
            {
                result.NextSteps = new List<string>
                {
                    "Spelling check passed - no spelling errors found"
                };
            }
            
            return result;
        }

        private async Task<CLICheckResponse> RunSnippetUpdate(string packagePath, CancellationToken ct = default)
        {
            logger.LogInformation("Running snippet update");

            var result = await languageChecks.UpdateSnippetsAsync(packagePath, ct);
            return result;
        }

        // Back-compat overload for callers/tests that don't pass a CancellationToken
        public Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType)
            => RunPackageCheck(packagePath, checkType, ct: default);
    }
}
