using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageRepoService(
    IProcessHelper processHelper,
    IGitHelper gitHelper,
    ILogger<GoLanguageRepoService> logger
) : LanguageRepoService(processHelper, gitHelper)
{
    private readonly string compilerName = "go";
    private readonly string compilerNameWindows = "go.exe";
    private readonly string formatterName = "goimports";
    private readonly string formatterNameWindows = "gofmt.exe";
    private readonly string linterName = "golangci-lint";
    private readonly string linterNameWindows = "golangci-lint.exe";

    #region Go specific functions, not part of the LanguageRepoService

    public async Task<bool> CheckDependencies(CancellationToken ct)
    {
        try
        {
            var compilerExists = (await _processHelper.Run(new(compilerName, ["version"], compilerNameWindows, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await _processHelper.Run(new(linterName, ["--version"], linterNameWindows, ["--version"]), ct)).ExitCode == 0;
            var formatterExists = (await _processHelper.Run(new("echo", ["package main", "|", formatterName]), ct)).ExitCode == 0;
            return compilerExists && linterExists && formatterExists;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<CLICheckResponse> CreateEmptyPackage(string packagePath, string moduleName, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new(compilerName, ["mod", "init", moduleName], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(CreateEmptyPackage));
            return new CLICheckResponse(1, "", $"{nameof(CreateEmptyPackage)} failed with an exception: {ex.Message}");
        }
    }

    #endregion

    public override async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            // Update all dependencies to the latest first
            var updateResult = await _processHelper.Run(new(compilerName, ["get", "-u", "all"], workingDirectory: packagePath), ct);
            if (updateResult.ExitCode != 0)
            {
                return CreateResponseFromProcessResult(updateResult);
            }

            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = await _processHelper.Run(new(compilerName, ["mod", "tidy"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(tidyResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependenciesAsync));
            return new CLICheckResponse(1, "", $"{nameof(AnalyzeDependenciesAsync)} failed with an exception: {ex.Message}");
        }
    }
    public override async Task<CLICheckResponse> FormatCodeAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new(
                formatterName, ["-w", "."],
                formatterNameWindows, ["-w", "."],
                workingDirectory: packagePath
            ), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCodeAsync));
            return new CLICheckResponse(1, "", $"{nameof(FormatCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public override async Task<CLICheckResponse> LintCodeAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new(linterName, ["run"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCodeAsync));
            return new CLICheckResponse(1, "", $"{nameof(LintCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public override async Task<CLICheckResponse> RunTestsAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new(compilerName, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunTestsAsync));
            return new CLICheckResponse(1, "", $"{nameof(RunTestsAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> BuildProjectAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new(compilerName, ["build"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProjectAsync));
            return new CLICheckResponse(1, "", $"{nameof(BuildProjectAsync)} failed with an exception: {ex.Message}");
        }
    }

    public override string GetSDKPackagePath(string repo, string packagePath)
    {
        if (!repo.EndsWith(Path.DirectorySeparatorChar))
        {
            repo += Path.DirectorySeparatorChar;
        }

        // ex: sdk/messaging/azservicebus
        return packagePath.Replace(repo, "");
    }
}
