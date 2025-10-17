using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public class GoLanguageSpecificChecks : ILanguageSpecificChecks
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

    public async Task<bool> CheckDependencies(CancellationToken ct)
    {
        try
        {
            var compilerExists = (await _processHelper.Run(new ProcessOptions(compilerName, ["version"], compilerNameWindows, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await _processHelper.Run(new ProcessOptions(linterName, ["--version"], linterNameWindows, ["--version"]), ct)).ExitCode == 0;
            var formatterExists = (await _processHelper.Run(new ProcessOptions("echo", ["package main", "|", formatterName]), ct)).ExitCode == 0;
            return compilerExists && linterExists && formatterExists;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception occurred while checking dependencies");
            return false;
        }
    }

    public async Task<PackageCheckResponse> CreateEmptyPackage(string packagePath, string moduleName, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["mod", "init", moduleName], compilerNameWindows, ["mod", "init", moduleName], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(CreateEmptyPackage));
            return new PackageCheckResponse(1, "", $"{nameof(CreateEmptyPackage)} failed with an exception: {ex.Message}");
        }
    }

    #endregion

    public async Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            // Update all dependencies to the latest first
            var updateResult = await _processHelper.Run(new ProcessOptions(compilerName, ["get", "-u", "all"], compilerNameWindows, ["get", "-u", "all"], workingDirectory: packagePath), ct);
            if (updateResult.ExitCode != 0)
            {
                return new PackageCheckResponse(updateResult);
            }

            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = await _processHelper.Run(new ProcessOptions(compilerName, ["mod", "tidy"], compilerNameWindows, ["mod", "tidy"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(tidyResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependenciesAsync));
            return new PackageCheckResponse(1, "", $"{nameof(AnalyzeDependenciesAsync)} failed with an exception: {ex.Message}");
        }
    }
    public async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(
                formatterName, ["-w", "."],
                formatterNameWindows, ["-w", "."],
                workingDirectory: packagePath
            ), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCodeAsync));
            return new PackageCheckResponse(1, "", $"{nameof(FormatCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(linterName, ["run"], linterNameWindows, ["run"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCodeAsync));
            return new PackageCheckResponse(1, "", $"{nameof(LintCodeAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> RunTests(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["test", "-v", "-timeout", "1h", "./..."], compilerNameWindows, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunTestsAsync));
            return new PackageCheckResponse(1, "", $"{nameof(RunTestsAsync)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> BuildProject(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await _processHelper.Run(new ProcessOptions(compilerName, ["build"], compilerNameWindows, ["build"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProject));
            return new PackageCheckResponse(1, "", $"{nameof(BuildProject)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<string> GetSDKPackageName(string repo, string packagePath, CancellationToken cancellationToken = default)
    {
        if (!repo.EndsWith(Path.DirectorySeparatorChar))
        {
            repo += Path.DirectorySeparatorChar;
        }

        // ex: sdk/messaging/azservicebus/
        var relativePath = packagePath.Replace(repo, "");
        // Ensure forward slashes for Go package names
        return await Task.FromResult(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
    }

    public async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new PackageCheckResponse());
    }

    public async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        // Implementation for validating CHANGELOG in a Python project
        // Could use markdownlint, etc.
        return await CommonLanguageHelpers.ValidateChangelogCommon(this, _processHelper, _gitHelper, _logger, packagePath, fixCheckErrors, cancellationToken);
    }
}

