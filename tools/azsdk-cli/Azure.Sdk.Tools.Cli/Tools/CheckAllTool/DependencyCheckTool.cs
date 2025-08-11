// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool runs dependency check for SDK projects.
    /// </summary>
    [Description("Run dependency check for SDK projects")]
    [McpServerToolType]
    public class DependencyCheckTool : MCPTool
    {
        private readonly ILogger<DependencyCheckTool> logger;
        private readonly IOutputService output;
        private readonly ILanguageRepoServiceFactory languageRepoServiceFactory;

        private readonly Option<string> packagePathOption = new(["--package-path", "-p"], "Path to the package directory to check") { IsRequired = true };

        public DependencyCheckTool(ILogger<DependencyCheckTool> logger, IOutputService output, ILanguageRepoServiceFactory languageRepoServiceFactory) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageRepoServiceFactory = languageRepoServiceFactory;
            CommandHierarchy = [SharedCommandGroups.Package, SharedCommandGroups.RunChecks];
        }

        public override Command GetCommand()
        {
            Command command = new("dependency-check", "Run dependency check for SDK projects");
            command.AddOption(packagePathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(packagePathOption);
            var result = await RunDependencyCheck(packagePath, ct);

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

    [McpServerTool(Name = "azsdk_package_run_check_dependency_check"), Description("Run dependency check for SDK packages. Provide absolute path to package root as param.")]
    public async Task<CLICheckResponse> RunDependencyCheck(string packagePath, CancellationToken ct)
        {
            try
            {
                logger.LogInformation($"Starting dependency check for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Create language service and run dependency analysis
                var languageService = languageRepoServiceFactory.CreateService(packagePath);
                logger.LogInformation($"Created language service: {languageService.GetType().Name}");
                
                var result = await languageService.AnalyzeDependenciesAsync(packagePath, ct);
                
                if (result.ExitCode != 0)
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(result.ExitCode, result.Output, "Dependency check failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running dependency check");
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }

        // Back-compat overload for callers/tests that don't pass a CancellationToken
        public Task<CLICheckResponse> RunDependencyCheck(string packagePath)
            => RunDependencyCheck(packagePath, ct: default);
    }
}
