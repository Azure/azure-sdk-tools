// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// This tool runs validation checks for SDK packages based on the specified check type.
    /// </summary>
    [Description("Run validation checks for SDK packages")]
    [McpServerToolType]
    public class PackageCheckTool(
        ILogger<PackageCheckTool> logger,
        ILanguageChecks languageChecks
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string RunChecksCommandName = "run-checks";

        private readonly Option<bool> fixOption = new(["--fix"], "Enable fix mode for supported checks (like spelling)") { IsRequired = false };

        protected override List<Command> GetCommands()
        {
            var parentCommand = new Command(RunChecksCommandName, "Run validation checks for SDK packages");
            // Add the package path option to the parent command so it can be used without subcommands
            parentCommand.AddOption(SharedOptions.PackagePath);
            parentCommand.AddOption(fixOption);

            var commands = new List<Command> { parentCommand };

            // Create sub-commands for each check type
            var checkTypeValues = Enum.GetValues<PackageCheckType>();
            foreach (var checkType in checkTypeValues)
            {
                var checkName = checkType.ToString().ToLowerInvariant();
                var subCommand = new Command(checkName, $"Run {checkName} validation check");
                subCommand.AddOption(SharedOptions.PackagePath);
                subCommand.AddOption(fixOption);

                parentCommand.AddCommand(subCommand);
                commands.Add(subCommand);
            }

            // Return all commands - parent and subcommands
            return commands;
        }

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // Get the command name which corresponds to the check type
            var command = ctx.ParseResult.CommandResult.Command;
            var commandName = command.Name;
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
            var fixCheckErrors = ctx.ParseResult.GetValueForOption(fixOption);

            // If this is the parent command (run-checks), default to All
            if (commandName == RunChecksCommandName)
            {
                return await RunPackageCheck(packagePath, PackageCheckType.All, fixCheckErrors, ct);
            }

            // Check if this is a subcommand by checking if its parent is the run-checks command
            if (command.Parents.Any(p => p.Name == RunChecksCommandName))
            {
                // Parse the subcommand name back to enum
                if (Enum.TryParse<PackageCheckType>(commandName, true, out var checkType))
                {
                    return await RunPackageCheck(packagePath, checkType, fixCheckErrors, ct);
                }
            }

            throw new ArgumentException($"Unknown command: {commandName}");
        }

        [McpServerTool(Name = "azsdk_package_run_check"), Description("Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors.")]
        public async Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting {checkType} check for package at: {packagePath}", checkType, packagePath);
                if (!Directory.Exists(packagePath))
                {
                    return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                return checkType switch
                {
                    PackageCheckType.All => await RunAllChecks(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Changelog => await RunChangelogValidation(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Dependency => await RunDependencyCheck(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Readme => await RunReadmeValidation(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Cspell => await RunSpellingValidation(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Snippets => await RunSnippetUpdate(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Linting => await RunLintCode(packagePath, fixCheckErrors, ct),
                    PackageCheckType.Format => await RunFormatCode(packagePath, fixCheckErrors, ct),
                    PackageCheckType.CheckAotCompat => await RunCheckAotCompat(packagePath, fixCheckErrors, ct),
                    PackageCheckType.GeneratedCodeChecks => await RunCheckGeneratedCode(packagePath, fixCheckErrors, ct),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(checkType),
                        checkType,
                        $"Unknown check type. Valid values are: {string.Join(", ", Enum.GetNames(typeof(PackageCheckType)))}")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package check");
                return new CLICheckResponse(1, ex.ToString(), $"Unhandled exception while running {checkType} check");
            }
        }

        private async Task<CLICheckResponse> RunAllChecks(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running all validation checks");

            var results = new List<CLICheckResponse>();
            var overallSuccess = true;
            var failedChecks = new List<string>();

            // Run dependency check
            var dependencyCheckResult = await languageChecks.AnalyzeDependenciesAsync(packagePath, fixCheckErrors, ct);
            results.Add(dependencyCheckResult);
            if (dependencyCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Dependency");
            }

            // Run changelog validation
            var changelogValidationResult = await languageChecks.ValidateChangelogAsync(packagePath, fixCheckErrors, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Changelog");
            }

            // Run README validation
            var readmeValidationResult = await languageChecks.ValidateReadmeAsync(packagePath, fixCheckErrors, ct);
            results.Add(readmeValidationResult);
            if (readmeValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("README");
            }

            // Run spelling check
            var spellingCheckResult = await languageChecks.CheckSpellingAsync(packagePath, fixCheckErrors, ct);
            results.Add(spellingCheckResult);
            if (spellingCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Spelling");
            }

            // Run snippet update
            var snippetUpdateResult = await languageChecks.UpdateSnippetsAsync(packagePath, fixCheckErrors, ct);
            results.Add(snippetUpdateResult);
            if (snippetUpdateResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Snippets");
            }

            // Run code linting
            var lintCodeResult = await languageChecks.LintCodeAsync(packagePath, fixCheckErrors, ct);
            results.Add(lintCodeResult);
            if (lintCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Linting");
            }

            // Run code formatting
            var formatCodeResult = await languageChecks.FormatCodeAsync(packagePath, fixCheckErrors, ct);
            results.Add(formatCodeResult);
            if (formatCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Format");
            }

            // Run AOT compatibility check
            var aotCompatResult = await languageChecks.CheckAotCompatAsync(packagePath, fixCheckErrors, ct);
            results.Add(aotCompatResult);
            if (aotCompatResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("AOT Compatibility");
            }

            // Run generated code check
            var generatedCodeResult = await languageChecks.CheckGeneratedCodeAsync(packagePath, fixCheckErrors, ct);
            results.Add(generatedCodeResult);
            if (generatedCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Generated Code");
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

        private async Task<CLICheckResponse> RunChangelogValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running changelog validation");

            var result = await languageChecks.ValidateChangelogAsync(packagePath, fixCheckErrors, ct);

            if (result.ExitCode != 0)
            {
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

        private async Task<CLICheckResponse> RunDependencyCheck(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running dependency check");

            var result = await languageChecks.AnalyzeDependenciesAsync(packagePath, fixCheckErrors, ct);

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

        private async Task<CLICheckResponse> RunReadmeValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running README validation");

            var result = await languageChecks.ValidateReadmeAsync(packagePath, fixCheckErrors, ct);

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

        private async Task<CLICheckResponse> RunSpellingValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running spelling validation{fixMode}", fixCheckErrors ? " with fix mode enabled" : "");

            var result = await languageChecks.CheckSpellingAsync(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunSnippetUpdate(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running snippet update");

            var result = await languageChecks.UpdateSnippetsAsync(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunLintCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running code linting");

            var result = await languageChecks.LintCodeAsync(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunFormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running code formatting");

            var result = await languageChecks.FormatCodeAsync(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunCheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running generated code checks");

            var result = await languageChecks.CheckGeneratedCodeAsync(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunCheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running AOT compatibility checks");

            var result = await languageChecks.CheckAotCompatAsync(packagePath, fixCheckErrors, ct);
            return result;
        }
    }
}
