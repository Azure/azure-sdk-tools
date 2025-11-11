using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Go-specific implementation of language repository service.
/// Uses tools like go build, go test, go mod, gofmt, etc. for Go development workflows.
/// </summary>
public partial class GoLanguageService : LanguageService
{    

    #region Go specific functions, not part of the LanguageRepoService

    public async Task<bool> CheckDependencies(CancellationToken ct)
    {
        try
        {
            var compilerExists = (await processHelper.Run(new ProcessOptions(compilerName, ["version"], compilerNameWindows, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await processHelper.Run(new ProcessOptions(linterName, ["--version"], linterNameWindows, ["--version"]), ct)).ExitCode == 0;
            var formatterExists = (await processHelper.Run(new ProcessOptions("echo", ["package main", "|", formatterName]), ct)).ExitCode == 0;
            return compilerExists && linterExists && formatterExists;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Exception occurred while checking dependencies");
            return false;
        }
    }

    public async Task<PackageCheckResponse> CreateEmptyPackage(string packagePath, string moduleName, CancellationToken ct)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(compilerName, ["mod", "init", moduleName], compilerNameWindows, ["mod", "init", moduleName], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(CreateEmptyPackage));
            return new PackageCheckResponse(1, "", $"{nameof(CreateEmptyPackage)} failed with an exception: {ex.Message}");
        }
    }

    #endregion

    public override async Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            // Update all dependencies to the latest first
            var updateResult = await processHelper.Run(new ProcessOptions(compilerName, ["get", "-u", "all"], compilerNameWindows, ["get", "-u", "all"], workingDirectory: packagePath), ct);
            if (updateResult.ExitCode != 0)
            {
                return new PackageCheckResponse(updateResult);
            }

            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = await processHelper.Run(new ProcessOptions(compilerName, ["mod", "tidy"], compilerNameWindows, ["mod", "tidy"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(tidyResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependencies));
            return new PackageCheckResponse(1, "", $"{nameof(AnalyzeDependencies)} failed with an exception: {ex.Message}");
        }
    }
    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(
                formatterName, ["-w", "."],
                formatterNameWindows, ["-w", "."],
                workingDirectory: packagePath
            ), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCode));
            return new PackageCheckResponse(1, "", $"{nameof(FormatCode)} failed with an exception: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(linterName, ["run"], linterNameWindows, ["run"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCode));
            return new PackageCheckResponse(1, "", $"{nameof(LintCode)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> BuildProject(string packagePath, CancellationToken ct)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(compilerName, ["build"], compilerNameWindows, ["build"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProject));
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
        // Ensure forward slashes for Go package names and remove trailing slash
        var packageName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        return await Task.FromResult(packageName.TrimEnd('/'));
    }

    public override async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new PackageCheckResponse());
    }

    public override async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var packageName = await GetSDKPackageName(repoRoot, packagePath, cancellationToken);
        return await commonValidationHelpers.ValidateChangelog(packageName, packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(compilerName, ["test", "-v", "-timeout", "1h", "./..."], compilerNameWindows, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunAllTests));
            return false;
        }
    }
}

