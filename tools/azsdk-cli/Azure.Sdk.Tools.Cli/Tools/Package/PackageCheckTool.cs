// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
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
        ILogger<LanguageMcpTool> logger,
        IGitHelper gitHelper,
        IEnumerable<LanguageService> languageServices
    ) : LanguageMcpTool(languageServices, logger: logger, gitHelper: gitHelper)
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [
            SharedCommandGroups.Package,
        ];

        private const string PackageRunCheckToolName = "azsdk_package_run_check";

        private readonly Argument<PackageCheckType> checkTypeArg = new("check-type")
        {
            Description = "Type of validation check to run",
            DefaultValueFactory = _ => PackageCheckType.All
        };

        private readonly Option<bool> fixOption = new("--fix")
        {
            Description = "Enable fix mode for supported checks (like spelling)",
            Required = false,
        };

        protected override Command GetCommand() =>
            new McpCommand("validate", "Run validation checks for SDK packages", PackageRunCheckToolName) { checkTypeArg, SharedOptions.PackagePath, fixOption };

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            // Get the command name which corresponds to the check type
            var command = parseResult.CommandResult.Command;
            var commandName = command.Name;
            var checkType = parseResult.GetValue(checkTypeArg);
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            var fixCheckErrors = parseResult.GetValue(fixOption);
            return await RunPackageCheck(packagePath, checkType, fixCheckErrors, ct);
        }

        [McpServerTool(Name = PackageRunCheckToolName), Description("Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors.")]
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
                overallSuccess = false;
                failedChecks.Add("Dependency");
            }
            else if (dependencyCheckResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Dependency");
            }

            // Run changelog validation
            var changelogValidationResult = await languageChecks.ValidateChangelog(packagePath, fixCheckErrors, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Changelog");
            }
            else if (changelogValidationResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Changelog");
            }

            // Run README validation
            var readmeValidationResult = await languageChecks.ValidateReadme(packagePath, fixCheckErrors, ct);
            results.Add(readmeValidationResult);
            if (readmeValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("README");
            }
            else if (readmeValidationResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("README");
            }

            // Run spelling check
            var spellingCheckResult = await languageChecks.CheckSpelling(packagePath, fixCheckErrors, ct);
            results.Add(spellingCheckResult);
            if (spellingCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Spelling");
            }
            else if (spellingCheckResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Spelling");
            }

            // Run snippet update
            var snippetUpdateResult = await languageChecks.UpdateSnippets(packagePath, fixCheckErrors, ct);
            results.Add(snippetUpdateResult);
            if (snippetUpdateResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Snippets");
            }
            else if (snippetUpdateResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Snippets");
            }

            // Run code linting
            var lintCodeResult = await languageChecks.LintCode(packagePath, fixCheckErrors, ct);
            results.Add(lintCodeResult);
            if (lintCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Linting");
            }
            else if (lintCodeResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Linting");
            }

            // Run code formatting
            var formatCodeResult = await languageChecks.FormatCode(packagePath, fixCheckErrors, ct);
            results.Add(formatCodeResult);
            if (formatCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Format");
            }
            else if (formatCodeResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Format");
            }

            // Run AOT compatibility check
            var aotCompatResult = await languageChecks.CheckAotCompat(packagePath, fixCheckErrors, ct);
            results.Add(aotCompatResult);
            if (aotCompatResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("AOT Compatibility");
            }
            else if (aotCompatResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("AOT Compatibility");
            }

            // Run generated code check
            var generatedCodeResult = await languageChecks.CheckGeneratedCode(packagePath, fixCheckErrors, ct);
            results.Add(generatedCodeResult);
            if (generatedCodeResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Generated Code");
            }
            else if (generatedCodeResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Generated Code");
            }
            // Run sample validation
            var sampleValidationResult = await languageChecks.ValidateSamples(packagePath, fixCheckErrors, ct);
            results.Add(sampleValidationResult);
            if (sampleValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
                failedChecks.Add("Sample Validation");
            }
            else if (sampleValidationResult.CheckStatusDetails != "noop")
            {
                successfulChecks.Add("Sample Validation");
            }

            var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
            
            // Create concise output - only show details for failed checks
            var outputParts = new List<string>();
            if (successfulChecks.Any())
            {
                outputParts.Add($"✓ Passed ({successfulChecks.Count}): {string.Join(", ", successfulChecks)}");
            }
            if (failedChecks.Any())
            {
                outputParts.Add($"✗ Failed ({failedChecks.Count}): {string.Join(", ", failedChecks)}");
                // Only include error details for failed checks
                var failedResults = results.Where(r => r.ExitCode != 0 && !string.IsNullOrWhiteSpace(r.Error)).ToList();
                if (failedResults.Any())
                {
                    outputParts.Add("\nErrors:");
                    foreach (var result in failedResults.Take(3)) // Limit to first 3 errors to avoid verbosity
                    {
                        outputParts.Add($"• {result.Error}");
                    }
                    if (failedResults.Count > 3)
                    {
                        outputParts.Add($"... and {failedResults.Count - 3} more error(s)");
                    }
                }
            }
            var combinedOutput = string.Join("\n", outputParts);

            // Generate concise next steps
            var nextSteps = new List<string>();
            if (overallSuccess)
            {
                nextSteps.Add("All validation checks passed - package is ready for next steps.");
            }
            else
            {
                nextSteps.Add($"Fix {failedChecks.Count} failed check(s): {string.Join(", ", failedChecks)}");
                nextSteps.Add("Re-run validation after fixes are applied.");
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
                result.NextSteps = new List<string> { "Update CHANGELOG.md to follow Azure SDK format guidelines" };
                return new PackageCheckResponse(result.ExitCode, result.CheckStatusDetails, "Changelog validation failed") { NextSteps = result.NextSteps };
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
                result.NextSteps = new List<string> { "Update dependencies to resolve conflicts and meet Azure SDK guidelines" };
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
                result.NextSteps = new List<string> { "Update README.md to meet Azure SDK documentation standards" };
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
    }
}
