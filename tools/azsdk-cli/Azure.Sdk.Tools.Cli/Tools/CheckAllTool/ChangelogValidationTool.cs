// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools
{
    /// <summary>
    /// This tool runs changelog validation for SDK projects.
    /// </summary>
    [Description("Run changelog validation for SDK projects")]
    [McpServerToolType]
    public class ChangelogValidationTool : MCPTool
    {
        private readonly ILogger<ChangelogValidationTool> logger;
        private readonly IOutputService output;
        private readonly ILanguageRepoServiceFactory languageRepoServiceFactory;

        public ChangelogValidationTool(ILogger<ChangelogValidationTool> logger, IOutputService output, ILanguageRepoServiceFactory languageRepoServiceFactory) : base()
        {
            this.logger = logger;
            this.output = output;
            this.languageRepoServiceFactory = languageRepoServiceFactory;
            CommandHierarchy = [SharedCommandGroups.Package, SharedCommandGroups.RunChecks];
        }

        public override Command GetCommand()
        {
            Command command = new("changelog-validation", "Run changelog validation for SDK projects");
            command.AddOption(SharedOptions.PackagePath);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
            var result = await RunChangelogValidation(packagePath);

            ctx.ExitCode = ExitCode;
            output.Output(result);
        }

        [McpServerTool(Name = "azsdk_package_run_check_changelog_validation"), Description("Run changelog validation for SDK packages. Provide absolute path to package root as param.")]
        public async Task<CLICheckResponse> RunChangelogValidation(string packagePath)
        {
            try
            {
                logger.LogInformation($"Starting changelog validation for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Create language service and run changelog validation
                var languageService = languageRepoServiceFactory.CreateService(packagePath);
                logger.LogInformation($"Created language service: {languageService.GetType().Name}");
                
                var result = await languageService.ValidateChangelogAsync(packagePath);
                
                if (result.ExitCode != 0)
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(result.ExitCode, result.Output, "Changelog validation failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while running changelog validation");
                SetFailure(1);
                return new FailureCLICheckResponse(1, ex.ToString(), "Unhandled exception while running changelog validation");
            }
        }
    }
}