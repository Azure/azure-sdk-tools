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

        [McpServerTool(Name = PackageRunCheckToolName), Description("Run validation checks for SDK packages. Provide package path, check type (All, Changelog, Dependency, Readme, Cspell, Snippets), and whether to fix errors. Note: --fix (fixCheckErrors=true) is not supported with check type 'All' because checks run in parallel; use a specific check type with --fix instead.")]
        public async Task<PackageCheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Starting {checkType} check for package at: {packagePath}", checkType, packagePath);
                if (!Directory.Exists(packagePath))
                {
                    return new PackageCheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                if (checkType == PackageCheckType.All && fixCheckErrors)
                {
                    return new PackageCheckResponse(
                        1,
                        "",
                        "--fix is not supported with check type 'All' because mutating checks run in parallel " +
                        "and can race on the same files. Run a specific check type (e.g., Cspell, Snippets, Format) with --fix instead.");
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            // --fix is disallowed at the entry point when checkType == All, so every check below is
            // read-only and safe to run in parallel. Fix mode must be invoked per-check-type.
            var checks = new (string Name, Func<Task<PackageCheckResponse>> Run)[]
            {
                ("Dependency",        () => languageChecks.AnalyzeDependencies(packagePath, false, ct)),
                ("Changelog",         () => languageChecks.ValidateChangelog(packagePath, false, ct)),
                ("README",            () => languageChecks.ValidateReadme(packagePath, false, ct)),
                ("Spelling",          () => languageChecks.CheckSpelling(packagePath, false, ct)),
                ("Snippets",          () => languageChecks.UpdateSnippets(packagePath, false, ct)),
                ("Linting",           () => languageChecks.LintCode(packagePath, false, ct)),
                ("Format",            () => languageChecks.FormatCode(packagePath, false, ct)),
                ("AOT Compatibility", () => languageChecks.CheckAotCompat(packagePath, false, ct)),
                ("Generated Code",    () => languageChecks.CheckGeneratedCode(packagePath, false, ct)),
                ("Sample Validation", () => languageChecks.ValidateSamples(packagePath, false, ct)),
            };

            var tasks = checks.Select(c => c.Run()).ToArray();
            var checkResults = await Task.WhenAll(tasks);

            var results = new List<PackageCheckResponse>();
            var failedChecks = new List<string>();
            var successfulChecks = new List<string>();
            var overallSuccess = true;

            for (int i = 0; i < checks.Length; i++)
            {
                var name = checks[i].Name;
                var result = checkResults[i];
                results.Add(result);
                if (result.ExitCode != 0)
                {
                    overallSuccess = false;
                    failedChecks.Add(name);
                }
                else if (result.CheckStatusDetails != "noop")
                {
                    successfulChecks.Add(name);
                }
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
                if (failedChecks.Any())
                {
                    nextSteps.Add($"Failed checks: {string.Join(", ", failedChecks)}");
                    nextSteps.Add("Address the issues identified above before proceeding with package release.");
                    nextSteps.Add("Re-run the package checks after making corrections to verify all issues are resolved.");
                }

                // Add specific guidance from individual check failures
                foreach (var result in results.Where(r => r.ExitCode != 0 && r.NextSteps?.Any() == true))
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            else if (result.CheckStatusDetails != "noop")
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            else if (result.CheckStatusDetails != "noop")
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            else if (result.CheckStatusDetails != "noop")
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
            if (languageChecks == null)
            {
                return CreateUnsupportedLanguageResponse(packagePath);
            }

            var result = await languageChecks.CheckSpelling(packagePath, fixCheckErrors, ct);

            if (result.ExitCode != 0 && (result.NextSteps == null || !result.NextSteps.Any()))
            {
                result.NextSteps = new List<string>
                {
                    "Run with --fix flag to automatically fix spelling errors using AI-assisted corrections",
                    "Add valid technical terms to the repo-root cspell configuration (e.g., .vscode/cspell.json)",
                    "Review the spelling errors and fix them manually in source files"
                };
            }
            else if (result.ExitCode == 0 && result.CheckStatusDetails != "noop")
            {
                result.NextSteps ??= new List<string>
                {
                    "Spelling check passed - no action needed"
                };
            }

            return result;
        }

        private async Task<PackageCheckResponse> RunSnippetUpdate(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
        {
            logger.LogInformation("Running snippet update");

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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

            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
            var languageChecks = await GetLanguageServiceAsync(packagePath, ct);
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
                var languageService = await GetLanguageServiceAsync(packagePath, ct);
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
