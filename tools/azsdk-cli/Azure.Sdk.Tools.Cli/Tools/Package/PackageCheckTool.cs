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
        private readonly LanguageChecks languageChecks;

        public PackageCheckTool(ILogger<PackageCheckTool> logger, IOutputHelper output, LanguageChecks languageChecks) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageChecks = languageChecks;
            CommandHierarchy = [SharedCommandGroups.Package];
        }

        public override Command GetCommand()
        {
            var parentCommand = new Command("run-checks", "Run validation checks for SDK packages");
            
            // Create sub-commands for each check type
            var checkTypeValues = Enum.GetValues<PackageCheckType>();
            foreach (var checkType in checkTypeValues)
            {
                var subCommand = new Command(checkType.ToString(), $"Run {checkType} validation check");
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
            
            // Parse the command name back to enum
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

        [McpServerTool(Name = "azsdk_package_run_check"), Description("Run validation checks for SDK packages. Provide package path and check type (All, Changelog, Dependency, Readme, Cspell).")]
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
                    PackageCheckType.Readme => await RunReadmeValidation(packagePath),
                    PackageCheckType.Cspell => await RunSpellingValidation(packagePath),
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

            // Run dependency check
            var dependencyCheckResult = await languageChecks.AnalyzeDependenciesAsync(packagePath, ct);
            results.Add(dependencyCheckResult);
            if (dependencyCheckResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            // Run changelog validation
            var changelogValidationResult = await languageChecks.ValidateChangelogAsync(packagePath, ct);
            results.Add(changelogValidationResult);
            if (changelogValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            // Run README validation
            var readmeValidationResult = await languageChecks.ValidateReadmeAsync(packagePath);
            results.Add(readmeValidationResult);
            if (readmeValidationResult.ExitCode != 0)
            {
                overallSuccess = false;
            }

            // Run spelling check
            var spellingCheckResult = await languageChecks.CheckSpellingAsync(packagePath);
            results.Add(spellingCheckResult);
            if (spellingCheckResult.ExitCode != 0)
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

        private async Task<CLICheckResponse> RunChangelogValidation(string packagePath, CancellationToken ct)
        {
            logger.LogInformation("Running changelog validation");

            var result = await languageChecks.ValidateChangelogAsync(packagePath, ct);

            if (result.ExitCode != 0)
            {
                SetFailure(1);
                return new CLICheckResponse(result.ExitCode, result.CheckStatusDetails, "Changelog validation failed");
            }

            return result;
        }

        private async Task<CLICheckResponse> RunDependencyCheck(string packagePath, CancellationToken ct)
        {
            logger.LogInformation("Running dependency check");

            var result = await languageChecks.AnalyzeDependenciesAsync(packagePath, ct);
            return result;
        }

        private async Task<CLICheckResponse> RunReadmeValidation(string packagePath)
        {
            logger.LogInformation("Running README validation");
            
            var result = await languageChecks.ValidateReadmeAsync(packagePath);
            return result;
        }

        private async Task<CLICheckResponse> RunSpellingValidation(string packagePath)
        {
            logger.LogInformation("Running spelling validation");
            
            var result = await languageChecks.CheckSpellingAsync(packagePath);
            return result;
        }

        // Back-compat overload for callers/tests that don't pass a CancellationToken
        public Task<CLICheckResponse> RunPackageCheck(string packagePath, PackageCheckType checkType)
            => RunPackageCheck(packagePath, checkType, ct: default);
    }
}
