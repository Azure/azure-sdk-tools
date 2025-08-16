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

namespace Azure.Sdk.Tools.Cli.Tools
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
        private readonly ILanguageRepoServiceFactory languageRepoServiceFactory;

        public PackageCheckTool(ILogger<PackageCheckTool> logger, IOutputHelper output, ILanguageRepoServiceFactory languageRepoServiceFactory) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageRepoServiceFactory = languageRepoServiceFactory;
            CommandHierarchy = [SharedCommandGroups.Package];
        }

        public override Command GetCommand()
        {
            Command command = new("run-checks", "Run validation checks for SDK packages");
            command.AddOption(SharedOptions.PackagePath);

            var checkTypeOption = new Option<PackageCheckName>(
                "--check-type",
                "The type of check to run")
            {
                IsRequired = true
            };
            command.AddOption(checkTypeOption);

            command.SetHandler(async (InvocationContext ctx) =>
            {
                var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
                var checkName = ctx.ParseResult.GetValueForOption(checkTypeOption);

                await HandleCommandWithOptions(packagePath, checkName, ctx.GetCancellationToken());
            });

            return command;
        }

        public override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // This method is required by the base class but not used since we handle commands directly in GetCommand
            throw new NotImplementedException("Command handling is done in GetCommand SetHandler");
        }

        private async Task HandleCommandWithOptions(string packagePath, PackageCheckName checkName, CancellationToken ct)
        {
            var result = await RunPackageCheck(packagePath, checkName, ct);

            ExitCode = result.ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "azsdk_package_run_check"), Description("Run validation checks for SDK packages. Provide package path and check type (All, Changelog, Dependency).")]
        public async Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckName checkName, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Starting {checkName} check for package at: {packagePath}");

                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Create language service
                ILanguageRepoService languageService;
                try
                {
                    languageService = languageRepoServiceFactory.GetService(packagePath);
                    logger.LogDebug($"Retrieved language service: {languageService.GetType().Name}");
                }
                catch (ArgumentException ex)
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Invalid package path: {ex.Message}");
                }
                catch (DirectoryNotFoundException ex)
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Package directory not found: {ex.Message}");
                }
                catch (NotSupportedException ex)
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Unsupported language: {ex.Message}");
                }
                catch (Exception ex)
                {
                    SetFailure(1);
                    logger.LogError(ex, "Failed to create language service");
                    return new CLICheckResponse(1, "", $"Unable to determine language for package at: {packagePath}. Error: {ex.Message}");
                }

                return checkName switch
                {
                    PackageCheckName.All => await RunAllChecks(packagePath, languageService, ct),
                    PackageCheckName.Changelog => await RunChangelogValidation(packagePath, languageService, ct),
                    PackageCheckName.Dependency => await RunDependencyCheck(packagePath, languageService, ct),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(checkName),
                        checkName,
                        $"Unknown check type. Valid values are: {string.Join(", ", Enum.GetNames(typeof(PackageCheckName)))}")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running package check");
                SetFailure(1);
                return new CLICheckResponse(1, ex.ToString(), $"Unhandled exception while running {checkName} check");
            }
        }

        private async Task<CLICheckResponse> RunAllChecks(string packagePath, ILanguageRepoService languageService, CancellationToken ct)
        {
            logger.LogInformation("Running all validation checks");

            var results = new List<CLICheckResponse>();
            var overallSuccess = true;

            // Run dependency check
            var dependencyCheckResult = await languageService.AnalyzeDependenciesAsync(packagePath, ct);
            results.Add(dependencyCheckResult);
            if (dependencyCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            // Run changelog validation
            var changelogValidationResult = await languageService.ValidateChangelogAsync(packagePath, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            if (!overallSuccess)
            {
                SetFailure(1);
            }

            var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
            var combinedOutput = string.Join("\n", results.Select(r => r.CheckStatusDetails));

            return overallSuccess
                ? new CLICheckResponse(0, combinedOutput)
                : new CLICheckResponse(1, combinedOutput, message);
        }

        private async Task<CLICheckResponse> RunChangelogValidation(string packagePath, ILanguageRepoService languageService, CancellationToken ct)
        {
            logger.LogInformation("Running changelog validation");

            var result = await languageService.ValidateChangelogAsync(packagePath, ct);

            if (result.ExitCode != 0)
            {
                SetFailure(1);
                return new CLICheckResponse(result.ExitCode, result.CheckStatusDetails, "Changelog validation failed");
            }

            return result;
        }

        private async Task<CLICheckResponse> RunDependencyCheck(string packagePath, ILanguageRepoService languageService, CancellationToken ct)
        {
            logger.LogInformation("Running dependency check");

            var result = await languageService.AnalyzeDependenciesAsync(packagePath, ct);

            if (result.ExitCode != 0)
            {
                SetFailure(1);
                return new CLICheckResponse(result.ExitCode, result.CheckStatusDetails, "Dependency check failed");
            }

            return result;
        }

        // Back-compat overload for callers/tests that don't pass a CancellationToken
        public Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckName checkName)
            => RunPackageCheck(packagePath, checkName, ct: default);
    }
}
