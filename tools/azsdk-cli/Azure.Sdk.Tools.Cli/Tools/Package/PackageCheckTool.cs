// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// This tool runs validation checks for SDK packages based on the specified check type.
    /// </summary>
    [Description("Run validation checks for SDK packages")]
    [McpServerToolType]
    public class PackageCheckTool(
        ILogger<LanguageMultiCommandTool> logger,
        IGitHelper gitHelper,
        IEnumerable<LanguageService> languageServices
    ) : LanguageMultiCommandTool(languageServices, logger: logger, gitHelper: gitHelper)
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string RunChecksCommandName = "run-checks";

        private readonly Option<bool> fixOption = new("--fix")
        {
            Description = "Enable fix mode for supported checks (like spelling)",
            Required = false,
        };

        protected override List<Command> GetCommands()
        {
            // Add the package path option to the parent command so it can be used without subcommands
            Command parentCommand = new(RunChecksCommandName, "Run validation checks for SDK packages") { SharedOptions.PackagePath, fixOption };

            // Create sub-commands for each check type
            var checkTypeValues = Enum.GetValues<PackageCheckType>();
            foreach (var checkType in checkTypeValues)
            {
                var checkName = checkType.ToString().ToLowerInvariant();
                Command subCommand = new(checkName, $"Run {checkName} validation check") { SharedOptions.PackagePath, fixOption };
                parentCommand.Subcommands.Add(subCommand);
            }

            // Return all commands - parent and subcommands so the handlers get registered
            return [parentCommand, .. parentCommand.Subcommands];
        }

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            // Get the command name which corresponds to the check type
            var command = parseResult.CommandResult.Command;
            var commandName = command.Name;
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            var fixCheckErrors = parseResult.GetValue(fixOption);

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
        public async Task<PackageCheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting {checkType} check for package at: {packagePath}", checkType, packagePath);
                if (!Directory.Exists(packagePath))
                {
                    return new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                var response = checkType switch
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
                    PackageCheckType.Samples => await RunSampleValidation(packagePath, fixCheckErrors, ct),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(checkType),
                        checkType,
                        $"Unknown check type. Valid values are: {string.Join(", ", Enum.GetNames(typeof(PackageCheckType)))}")
                };
                await AddPackageDetailsInResponse(response, packagePath, ct);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package check");
                return new PackageCheckResponse(1, ex.ToString(), $"Unhandled exception while running {checkType} check");
            }
        }

        private async Task<PackageCheckResponse> RunAllChecks(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running all validation checks");

            var results = new List<PackageCheckResponse>();
            var overallSuccess = true;
            var failedChecks = new List<string>();
            var successfulChecks = new List<string>();
            var notImplementedChecks = new List<string>();

            // Run dependency check
            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var dependencyCheckResult = await languageChecks.AnalyzeDependencies(packagePath, fixCheckErrors, ct);
            results.Add(dependencyCheckResult);
            if (dependencyCheckResult.ExitCode != 0)
            {
                if (IsNotImplemented(dependencyCheckResult))
                {
                    notImplementedChecks.Add("Dependency");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Dependency");
                }
            }
            else
            {
                successfulChecks.Add("Dependency");
            }

            // Run changelog validation
            var changelogValidationResult = await languageChecks.ValidateChangelog(packagePath, fixCheckErrors, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                if (IsNotImplemented(changelogValidationResult))
                {
                    notImplementedChecks.Add("Changelog");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Changelog");
                }
            }
            else
            {
                successfulChecks.Add("Changelog");
            }

            // Run README validation
            var readmeValidationResult = await languageChecks.ValidateReadme(packagePath, fixCheckErrors, ct);
            results.Add(readmeValidationResult);
            if (readmeValidationResult.ExitCode != 0)
            {
                if (IsNotImplemented(readmeValidationResult))
                {
                    notImplementedChecks.Add("README");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("README");
                }
            }
            else
            {
                successfulChecks.Add("README");
            }

            // Run spelling check
            var spellingCheckResult = await languageChecks.CheckSpelling(packagePath, fixCheckErrors, ct);
            results.Add(spellingCheckResult);
            if (spellingCheckResult.ExitCode != 0)
            {
                if (IsNotImplemented(spellingCheckResult))
                {
                    notImplementedChecks.Add("Spelling");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Spelling");
                }
            }
            else
            {
                successfulChecks.Add("Spelling");
            }

            // Run snippet update
            var snippetUpdateResult = await languageChecks.UpdateSnippets(packagePath, fixCheckErrors, ct);
            results.Add(snippetUpdateResult);
            if (snippetUpdateResult.ExitCode != 0)
            {
                if (IsNotImplemented(snippetUpdateResult))
                {
                    notImplementedChecks.Add("Snippets");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Snippets");
                }
            }
            else
            {
                successfulChecks.Add("Snippets");
            }

            // Run code linting
            var lintCodeResult = await languageChecks.LintCode(packagePath, fixCheckErrors, ct);
            results.Add(lintCodeResult);
            if (lintCodeResult.ExitCode != 0)
            {
                if (IsNotImplemented(lintCodeResult))
                {
                    notImplementedChecks.Add("Linting");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Linting");
                }
            }
            else
            {
                successfulChecks.Add("Linting");
            }

            // Run code formatting
            var formatCodeResult = await languageChecks.FormatCode(packagePath, fixCheckErrors, ct);
            results.Add(formatCodeResult);
            if (formatCodeResult.ExitCode != 0)
            {
                if (IsNotImplemented(formatCodeResult))
                {
                    notImplementedChecks.Add("Format");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Format");
                }
            }
            else
            {
                successfulChecks.Add("Format");
            }

            // Run AOT compatibility check
            var aotCompatResult = await languageChecks.CheckAotCompat(packagePath, fixCheckErrors, ct);
            results.Add(aotCompatResult);
            if (aotCompatResult.ExitCode != 0)
            {
                if (IsNotImplemented(aotCompatResult))
                {
                    notImplementedChecks.Add("AOT Compatibility");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("AOT Compatibility");
                }
            }
            else
            {
                successfulChecks.Add("AOT Compatibility");
            }

            // Run generated code check
            var generatedCodeResult = await languageChecks.CheckGeneratedCode(packagePath, fixCheckErrors, ct);
            results.Add(generatedCodeResult);
            if (generatedCodeResult.ExitCode != 0)
            {
                if (IsNotImplemented(generatedCodeResult))
                {
                    notImplementedChecks.Add("Generated Code");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Generated Code");
                }
            }
            else
            {
                successfulChecks.Add("Generated Code");
            }
            // Run sample validation
            var sampleValidationResult = await languageChecks.ValidateSamples(packagePath, fixCheckErrors, ct);
            results.Add(sampleValidationResult);
            if (sampleValidationResult.ExitCode != 0)
            {
                if (IsNotImplemented(sampleValidationResult))
                {
                    notImplementedChecks.Add("Sample Validation");
                }
                else
                {
                    overallSuccess = false;
                    failedChecks.Add("Sample Validation");
                }
            }
            else
            {
                successfulChecks.Add("Sample Validation");
            }

            var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
            var combinedOutput = string.Join("\n", results.Select(r => r.CheckStatusDetails));

            // Generate comprehensive next steps for all checks
            var nextSteps = new List<string>();
            if (overallSuccess)
            {
                nextSteps.Add("All package validation checks passed! Your package is ready for the next steps in the development process.");
                if (successfulChecks.Any())
                {
                    nextSteps.Add($"Successful checks: {string.Join(", ", successfulChecks)}");
                }
                if (notImplementedChecks.Any())
                {
                    nextSteps.Add($"Note: The following checks are not implemented for this language: {string.Join(", ", notImplementedChecks)}");
                }
                nextSteps.Add("Consider running package release readiness checks if preparing for release.");
            }
            else
            {
                if (successfulChecks.Any())
                {
                    nextSteps.Add($"Successful checks: {string.Join(", ", successfulChecks)}");
                }
                if (failedChecks.Any())
                {
                    nextSteps.Add($"Failed checks: {string.Join(", ", failedChecks)}");
                    nextSteps.Add("Address the issues identified above before proceeding with package release.");
                    nextSteps.Add("Re-run the package checks after making corrections to verify all issues are resolved.");
                }
                
                if (notImplementedChecks.Any())
                {
                    logger.LogDebug("Note: The following checks are not implemented for this language: {NotImplementedChecks}", string.Join(", ", notImplementedChecks));
                }

                // Add specific guidance from individual check failures (exclude not implemented ones)
                foreach (var result in results.Where(r => r.ExitCode != 0 && !IsNotImplemented(r) && r.NextSteps?.Any() == true))
                {
                    nextSteps.AddRange(result.NextSteps);
                }
            }

            return overallSuccess
                ? new PackageCheckResponse(0, combinedOutput) { NextSteps = nextSteps }
                : new PackageCheckResponse(1, combinedOutput, message) { NextSteps = nextSteps };
        }

        private async Task<PackageCheckResponse> RunChangelogValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running changelog validation");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.ValidateChangelog(packagePath, fixCheckErrors, ct);

            if (result.ExitCode != 0)
            {
                result.NextSteps = new List<string>
                {
                    "Review and update the CHANGELOG.md file to ensure it follows the proper format",
                    "Verify that unreleased changes are properly documented",
                    "Check that version numbers and release dates are correctly formatted",
                    "Refer to the Azure SDK changelog guidelines for proper formatting"
                };
                return new PackageCheckResponse(result.ExitCode, result.CheckStatusDetails, "Changelog validation failed") { NextSteps = result.NextSteps };
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

        private async Task<PackageCheckResponse> RunDependencyCheck(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running dependency check");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.AnalyzeDependencies(packagePath, fixCheckErrors, ct);

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

        private async Task<PackageCheckResponse> RunReadmeValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running README validation");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.ValidateReadme(packagePath, fixCheckErrors, ct);

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

        private async Task<PackageCheckResponse> RunSpellingValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running spelling validation{fixMode}", fixCheckErrors ? " with fix mode enabled" : "");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.CheckSpelling(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunSnippetUpdate(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running snippet update");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.UpdateSnippets(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunLintCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running code linting");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.LintCode(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunFormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running code formatting");

            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.FormatCode(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunCheckGeneratedCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running generated code checks");
            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.CheckGeneratedCode(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunCheckAotCompat(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running AOT compatibility checks");
            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.CheckAotCompat(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task<PackageCheckResponse> RunSampleValidation(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running sample validation");
            var languageChecks = GetLanguageService(packagePath);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.ValidateSamples(packagePath, fixCheckErrors, ct);
            return result;
        }

        private async Task AddPackageDetailsInResponse(PackageCheckResponse response, string packagePath, CancellationToken ct)
        {
            try
            {
                var languageService = GetLanguageService(packagePath);
                if (languageService != null)
                {
                    var info = await languageService.GetPackageInfo(packagePath, ct);
                    response.PackageName = info.PackageName;
                    response.Version = info.PackageVersion;
                    response.PackageType = info.SdkType;
                    response.Language = info.Language;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AddPackageDetailsInResponse");
            }
        }        

        private static PackageCheckResponse CreateUnsupportedLanguageResponse(string packagePath)
        {
            return new PackageCheckResponse(
                exitCode: 1,
                checkStatusDetails: $"No language-specific check handler found for package at {packagePath}. Supported languages may not include this package type.",
                error: "Unsupported package type"
            );
        }

        private static bool IsNotImplemented(PackageCheckResponse response)
        {
            return response.ResponseError != null && response.ResponseError.Contains("not implemented", StringComparison.OrdinalIgnoreCase);
        }
    }
}
