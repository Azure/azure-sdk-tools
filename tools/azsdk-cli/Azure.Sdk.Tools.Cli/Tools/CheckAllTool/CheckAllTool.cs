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

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool runs all the check/validation tools at once to provide comprehensive validation.
    /// </summary>
    [Description("Run all validation checks for SDK projects")]
    [McpServerToolType]
    public partial class CheckAllTool : MCPTool
    {
        private readonly ILogger<CheckAllTool> logger;
        private readonly IOutputService output;
        private readonly ILanguageRepoServiceFactory languageRepoServiceFactory;


        public override Command GetCommand()
        {
            Command command = new("all", "Run all validation checks for SDK projects");
            command.AddOption(SharedOptions.PackagePath);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var pr = ctx.ParseResult;
            var packagePath = pr.GetValueForOption(SharedOptions.PackagePath);
            var result = await RunAllChecks(packagePath, ct);

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        public CheckAllTool(
            ILogger<CheckAllTool> logger, 
            IOutputService output,
            ILanguageRepoServiceFactory languageRepoServiceFactory) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageRepoServiceFactory = languageRepoServiceFactory;
            CommandHierarchy = [SharedCommandGroups.Package, SharedCommandGroups.RunChecks];
        }

        

        [McpServerTool(Name = "azsdk_package_run_all_checks"), Description("Run all validation checks for SDK packages. Provide absolute path to package root as param.")]
    public async Task<CLICheckResponse> RunAllChecks(string packagePath, CancellationToken ct)
        {
            try
            {
                logger.LogInformation($"Starting all checks for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new CLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                var results = new List<CLICheckResponse>();
                var overallSuccess = true;

                // Create language service to run checks instead of instantiating tools directly
                var languageService = languageRepoServiceFactory.CreateService(packagePath);
                logger.LogInformation($"Created language service: {languageService.GetType().Name}");

                // Run dependency check using language service
                var dependencyCheckResult = await languageService.AnalyzeDependenciesAsync(packagePath, ct);
                results.Add(dependencyCheckResult);
                if (dependencyCheckResult.ExitCode != 0)
                {
                    overallSuccess = false;
                }

                // Run changelog validation using language service
                var changelogValidationResult = await languageService.ValidateChangelogAsync(packagePath);
                results.Add(changelogValidationResult);
                if (changelogValidationResult.ExitCode != 0)
                {
                    overallSuccess = false;
                }

                if (!overallSuccess) { SetFailure(1); }

                var message = overallSuccess ? "All checks completed successfully" : "Some checks failed";
                var combinedOutput = string.Join("\n", results.Select(r => r.Output));
                
                return overallSuccess 
                    ? new SuccessCLICheckResponse(0, combinedOutput) 
                    : new CLICheckResponse(1, combinedOutput, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running checks");
                SetFailure(1);
                return new CLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }

        // Back-compat overload for callers/tests that don't pass a CancellationToken
        public Task<CLICheckResponse> RunAllChecks(string packagePath)
            => RunAllChecks(packagePath, ct: default);

    }
}