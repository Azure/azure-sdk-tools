// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine.Invocation;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Commands;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Tools;

[Description("Run validation checks for SDK projects")]
[McpServerToolType]
public partial class UnifiedCheckTool : MCPTool
{
    private readonly ILanguageRepoServiceFactory languageRepoServiceFactory;
    private readonly ILogger<UnifiedCheckTool> logger;
    private readonly IOutputService output;

    public UnifiedCheckTool(ILanguageRepoServiceFactory languageRepoServiceFactory, ILogger<UnifiedCheckTool> logger, IOutputService output)
    {
        this.languageRepoServiceFactory = languageRepoServiceFactory;
        this.logger = logger;
        this.output = output;
    }

    public override Command GetCommand()
    {
        // This tool is primarily for MCP use, so return a simple command structure
        Command command = new("consolidated-check", "Run validation checks for SDK projects");
        command.AddOption(SharedOptions.PackagePath);
        command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return command;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var packagePath = ctx.ParseResult.GetValueForOption(SharedOptions.PackagePath);
        var result = await RunCheck(packagePath, "all", ct);

        ctx.ExitCode = ExitCode;
        output.Output(result);
    }

    [McpServerTool(Name = "azsdk_package_run_check"), Description("Run validation checks for SDK packages. Provide absolute path to package root and check name (all, changelog-validation, dependency-check).")]
    public async Task<CLICheckResponse> RunCheck(string packagePath, string checkName, CancellationToken ct)
    {
        try
        {
            logger.LogInformation($"Starting {checkName} check for package at: {packagePath}");
            
            if (string.IsNullOrEmpty(packagePath))
            {
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", "Package path cannot be null or empty");
            }

            if (!Directory.Exists(packagePath))
            {
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
            }

            if (string.IsNullOrEmpty(checkName))
            {
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", "Check name cannot be null or empty");
            }

            // Create language service to run checks
            var languageService = languageRepoServiceFactory.CreateService(packagePath);
            
            return checkName.ToLowerInvariant() switch
            {
                "all" => await RunAllChecks(languageService, packagePath, ct),
                "changelog-validation" => await RunChangelogValidation(languageService, packagePath, ct),
                "dependency-check" => await RunDependencyCheck(languageService, packagePath, ct),
                _ => new FailureCLICheckResponse(1, "", $"Unknown check name: {checkName}. Valid options are: all, changelog-validation, dependency-check")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RunCheck failed with an exception");
            SetFailure(1);
            return new FailureCLICheckResponse(1, "", $"Check failed: {ex.Message}");
        }
    }

    private async Task<CLICheckResponse> RunAllChecks(ILanguageRepoService languageService, string packagePath, CancellationToken ct)
    {
        var results = new List<CLICheckResponse>();
        var overallSuccess = true;

        // Run changelog validation
        var changelogResult = await languageService.ValidateChangelogAsync(packagePath);
        results.Add(changelogResult);
        if (changelogResult.ExitCode != 0)
        {
            overallSuccess = false;
        }

        // Run dependency check
        var dependencyResult = await languageService.AnalyzeDependenciesAsync(packagePath, ct);
        results.Add(dependencyResult);
        if (dependencyResult.ExitCode != 0)
        {
            overallSuccess = false;
        }

        if (overallSuccess)
        {
            var successOutput = string.Join("\n", results.Select(r => r.Output));
            return new SuccessCLICheckResponse(0, successOutput);
        }
        else
        {
            SetFailure(1);
            var failureOutput = string.Join("\n", results.Select(r => r.Output));
            var errorDetails = string.Join("; ", results.Where(r => r.ExitCode != 0).OfType<FailureCLICheckResponse>().Select(r => r.Error));
            return new FailureCLICheckResponse(1, failureOutput, errorDetails);
        }
    }

    private async Task<CLICheckResponse> RunChangelogValidation(ILanguageRepoService languageService, string packagePath, CancellationToken ct)
    {
        var result = await languageService.ValidateChangelogAsync(packagePath);
        if (result.ExitCode != 0)
        {
            SetFailure(result.ExitCode);
        }
        return result;
    }

    private async Task<CLICheckResponse> RunDependencyCheck(ILanguageRepoService languageService, string packagePath, CancellationToken ct)
    {
        var result = await languageService.AnalyzeDependenciesAsync(packagePath, ct);
        if (result.ExitCode != 0)
        {
            SetFailure(result.ExitCode);
        }
        return result;
    }
}