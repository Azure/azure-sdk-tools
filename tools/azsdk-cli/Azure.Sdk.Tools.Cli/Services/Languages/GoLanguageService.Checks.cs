using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

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
            var compilerExists = (await processHelper.Run(new ProcessOptions(goUnix, ["version"], goWin, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await processHelper.Run(new ProcessOptions(golangciLintUnix, ["--version"], golangciLintWin, ["--version"]), ct)).ExitCode == 0;

            var tempFilePath = Path.GetTempFileName();
            bool formatterExists = false;

            try
            {
                await File.WriteAllTextAsync(tempFilePath, "package main", ct);
                var gofmtOptions = new ProcessOptions(
                    unixCommand: gofmtUnix, unixArgs: [tempFilePath],
                    windowsCommand: gofmtWin, windowsArgs: [tempFilePath]
                );
                formatterExists = (await processHelper.Run(gofmtOptions, ct)).ExitCode == 0;
            }
            finally
            {
                File.Delete(tempFilePath);
            }

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
            var result = await processHelper.Run(new ProcessOptions(goUnix, ["mod", "init", moduleName], goWin, ["mod", "init", moduleName], workingDirectory: packagePath), ct);
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
            var goGetArgs = new List<string>(["get", "-u", "all"]);
            var goModVersion = await GetGoModVersionAsync(Path.Join(packagePath, "go.mod"), ct);

            if (goModVersion.Major == 1 && goModVersion.Minor == 23)
            {
                // For compatibility, we'll ensure that the toolchain/go-version does not upgrade for modules 
                // that are still set at 1.23. See this issue for some context:
                //   https://github.com/Azure/azure-sdk-for-go/issues/25407
                goGetArgs.AddRange(["toolchain@none", "go@1.23.0"]);
            }

            // Update all dependencies to the latest first
            var updateResult = await processHelper.Run(new ProcessOptions(goUnix, [.. goGetArgs], goWin, [.. goGetArgs], workingDirectory: packagePath), ct);
            if (updateResult.ExitCode != 0)
            {
                return new PackageCheckResponse(updateResult);
            }

            // Now tidy, to cleanup any deps that aren't needed
            var tidyResult = await processHelper.Run(new ProcessOptions(goUnix, ["mod", "tidy"], goWin, ["mod", "tidy"], workingDirectory: packagePath), ct);
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
                gofmtUnix, ["-w", "."],
                gofmtWin, ["-w", "."],
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
            var result = await processHelper.Run(new ProcessOptions(golangciLintUnix, ["run"], golangciLintWin, ["run"], workingDirectory: packagePath), ct);
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
            var result = await processHelper.Run(new ProcessOptions(goUnix, ["build"], goWin, ["build"], workingDirectory: packagePath), ct);
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
            var result = await processHelper.Run(new ProcessOptions(goUnix, ["test", "-v", "-timeout", "1h", "./..."], goWin, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunAllTests));
            return false;
        }
    }

    /// <summary>
    /// Gets the version specified by the go version directive in the go.mod file.
    /// </summary>
    /// <param name="goModPath">Path to a go.mod file</param>
    /// <param name="ct"></param>
    public static async Task<Version> GetGoModVersionAsync(string goModPath, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(goModPath, ct);

        var match = GoModVersionRegex().Match(text);

        if (!match.Success)
        {
            throw new Exception($"{goModPath} doesn't contain a go version directive");
        }

        return Version.Parse(match.Groups[1].Value);
    }

    /// <summary>
    /// Captures the go version directive in a go.mod file
    /// </summary>
    /// <remarks>
    /// Ex: "go 1.24.0", "go 1.23", etc...
    /// </remarks>
    [GeneratedRegex(@"^go (1\.\d+(?:\.\d+|$))", RegexOptions.Multiline)]
    private static partial Regex GoModVersionRegex();
}

