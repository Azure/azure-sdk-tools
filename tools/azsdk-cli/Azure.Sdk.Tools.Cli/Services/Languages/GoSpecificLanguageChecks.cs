using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageSpecificChecks : ILanguageSpecificCheck
{
    private readonly IProcessHelper _processHelper;
    private readonly INpxHelper _npxHelper;
    private readonly IGitHelper _gitHelper;
    private readonly ILogger<GoLanguageSpecificChecks> _logger;

    public GoLanguageSpecificChecks(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<GoLanguageSpecificChecks> logger)
    {
        _processHelper = processHelper;
        _npxHelper = npxHelper;
        _gitHelper = gitHelper;
        _logger = logger;
    }
    private readonly string compilerName = "go";
    private readonly string compilerNameWindows = "go.exe";
    private readonly string formatterName = "goimports";
    private readonly string formatterNameWindows = "gofmt.exe";
    private readonly string linterName = "golangci-lint";
    private readonly string linterNameWindows = "golangci-lint.exe";

    #region Go specific functions, not part of the LanguageRepoService

    public string SupportedLanguage => "Go";

    public bool CanHandle(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
        {
            return false;
        }

        var repositoryPath = _gitHelper.DiscoverRepoRoot(packagePath);

        // Get the repository name from the directory path
        var repoName = Path.GetFileName(repositoryPath?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.ToLowerInvariant() ?? "";

        _logger.LogInformation($"Repository name: {repoName}");

        // Extract the language from the repository name
        if (repoName.Contains("azure-sdk-for-go"))
        {
            _logger.LogInformation("Detected language: go from repository name");
            return true;
        }
        return false;
    }

    public async Task<bool> CheckDependencies(CancellationToken ct)
    {
        try
        {
            var compilerExists = (await _processHelper.Run(new ProcessOptions(compilerName, ["version"], compilerNameWindows, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await _processHelper.Run(new ProcessOptions(linterName, ["--version"], linterNameWindows, ["--version"]), ct)).ExitCode == 0;
            var formatterExists = (await _processHelper.Run(new ProcessOptions("echo", ["package main", "|", formatterName]), ct)).ExitCode == 0;
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
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["mod", "init", moduleName], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(CreateEmptyPackage));
            return new CLICheckResponse(1, "", $"{nameof(CreateEmptyPackage)} failed with an exception: {ex.Message}");
        }
    }

    #endregion

    public async Task<CLICheckResponse> AnalyzeDependenciesAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            // Update all dependencies to the latest first
            var updateResult = await _processHelper.Run(new ProcessOptions(compilerName, ["get", "-u", "all"], workingDirectory: packagePath), ct);
            if (updateResult.ExitCode != 0)
            {
                return CreateResponseFromProcessResult(updateResult);
            }

            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = await _processHelper.Run(new ProcessOptions(compilerName, ["mod", "tidy"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(tidyResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependenciesAsync));
            return new CLICheckResponse(1, "", $"{nameof(AnalyzeDependenciesAsync)} failed with an exception: {ex.Message}");
        }
    }
    public async Task<CLICheckResponse> FormatCodeAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(
                formatterName, ["-w", "."],
                formatterNameWindows, ["-w", "."],
                workingDirectory: packagePath
            ), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCodeAsync));
            return new CLICheckResponse(1, "", $"{nameof(FormatCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> LintCodeAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(linterName, ["run"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCodeAsync));
            return new CLICheckResponse(1, "", $"{nameof(LintCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> RunTestsAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunTestsAsync));
            return new CLICheckResponse(1, "", $"{nameof(RunTestsAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<CLICheckResponse> BuildProjectAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["build"], workingDirectory: packagePath), ct);
            return CreateResponseFromProcessResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProjectAsync));
            return new CLICheckResponse(1, "", $"{nameof(BuildProjectAsync)} failed with an exception: {ex.Message}");
        }
    }

    public string GetSDKPackagePath(string repo, string packagePath)
    {
        if (!repo.EndsWith(Path.DirectorySeparatorChar))
        {
            repo += Path.DirectorySeparatorChar;
        }

        // ex: sdk/messaging/azservicebus
        return packagePath.Replace(repo, "");
    }

    /// <summary>
    /// Creates a CLI check response from a process result
    /// </summary>
    /// <param name="processResult">The process result to convert</param>
    /// <returns>CLI check response</returns>
    private CLICheckResponse CreateResponseFromProcessResult(ProcessResult processResult)
    {
        return new CLICheckResponse(processResult.ExitCode, processResult.Output);
    }
}