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

    private string goUnix => "go";
    private string goWin => "go.exe";
    private string gofmtUnix => "gofmt";
    private string gofmtWin => "gofmt.exe";
    private string golangciLintUnix => "golangci-lint";
    private string golangciLintWin => "golangci-lint.exe";

    public async Task<bool> CheckDependencies(CancellationToken ct)
    {
        try
        {
            var compilerExists = (await processHelper.Run(new ProcessOptions(goUnix, goWin, ["version"]), ct)).ExitCode == 0;
            var linterExists = (await processHelper.Run(new ProcessOptions(golangciLintUnix, golangciLintWin, ["--version"]), ct)).ExitCode == 0;

            var tempFilePath = Path.GetTempFileName();
            bool formatterExists = false;

            try
            {
                await File.WriteAllTextAsync(tempFilePath, "package main", ct);
                var gofmtOptions = new ProcessOptions(gofmtUnix, gofmtWin, [tempFilePath]);
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

    public async Task CreateEmptyPackage(string packagePath, string moduleName, CancellationToken ct)
    {
        var result = await processHelper.Run(new ProcessOptions(goUnix, goWin, ["mod", "init", moduleName], workingDirectory: packagePath), ct);

        if (result.ExitCode != 0)
        {
            throw new Exception($"Failed to create empty Go package: {result.Output}");
        }
    }

    #endregion

    public override async Task<PackageCheckResponse> AnalyzeDependencies(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        var results = new List<ProcessResult>();
        string packageName;

        try
        {
            packageName = await GetSubPath(packagePath, ct);
        }
        catch (Exception ex)
        {
            // NOTE: packagePath is checked for null/empty already in PackageCheckTool, so it should be safe to use it.
            logger.LogError(ex, "{MethodName} failed with an exception when trying to get the and repo root/package name for package path {packagePath}", nameof(AnalyzeDependencies), packagePath);
            return new PackageCheckResponse(packagePath, Models.SdkLanguage.Go, 1, "", $"{nameof(AnalyzeDependencies)} failed with an exception when trying to get the and repo root/package name for package path {packagePath}: {ex.Message}");
        }

        try
        {
            // in some modules we have two go.mod files (or multiple). We want to update those as well.
            var goModFiles = Directory.EnumerateFiles(packagePath, "go.mod", SearchOption.AllDirectories).ToArray();

            logger.LogInformation("Found go.mod files in project:{goModFiles}", string.Join(",", goModFiles));

            foreach (var goModPath in goModFiles)
            {
                var goModDir = Path.GetDirectoryName(goModPath);
                var goGetArgs = new List<string>(["get", "-u", "all"]);
                var goModVersion = await GetGoModVersionAsync(goModPath, ct);

                if (goModVersion.Major == 1 && goModVersion.Minor == 23)
                {
                    // For compatibility, we'll ensure that the toolchain/go-version does not upgrade for modules
                    // that are still set at 1.23. See this issue for some context:
                    //   https://github.com/Azure/azure-sdk-for-go/issues/25407
                    goGetArgs.AddRange(["toolchain@none", "go@1.23.0"]);
                }

                // Update all dependencies to the latest first
                var updateResult = await processHelper.Run(new ProcessOptions(goUnix, goWin, [.. goGetArgs], workingDirectory: goModDir), ct);
                results.Add(updateResult);

                if (updateResult.ExitCode != 0)
                {
                    break;
                }

                // Now tidy, to cleanup any deps that aren't needed
                var tidyResult = await processHelper.Run(new ProcessOptions(goUnix, goWin, ["mod", "tidy"], workingDirectory: goModDir), ct);
                results.Add(tidyResult);

                if (tidyResult.ExitCode != 0)
                {
                    break;
                }
            }

            return new PackageCheckResponse(packageName, Models.SdkLanguage.Go, results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(AnalyzeDependencies));
            return new PackageCheckResponse(packageName, Models.SdkLanguage.Go, 1, "", $"{nameof(AnalyzeDependencies)} failed with an exception: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> FormatCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(gofmtUnix, gofmtWin, ["-w", "."], workingDirectory: packagePath), ct);

            var packageName = await GetSubPath(packagePath, ct);
            return new PackageCheckResponse(packageName, Models.SdkLanguage.Go, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(FormatCode));
            return new PackageCheckResponse(packagePath, Models.SdkLanguage.Go, 1, "", $"{nameof(FormatCode)} failed with an exception: {ex.Message}");
        }
    }

    public override async Task<PackageCheckResponse> LintCode(string packagePath, bool fixCheckErrors = false, CancellationToken ct = default)
    {
        try
        {
            var processResults = new List<ProcessResult>();

            // run the standard golangci-lint
            var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
            var packageName = await GetSubPath(packagePath, ct);
            var result = await processHelper.Run(new ProcessOptions(golangciLintUnix, golangciLintWin, ["run", "--config", Path.Join(repoRoot, "eng", ".golangci.yml")], workingDirectory: packagePath), ct);
            processResults.Add(result);

            if (result.ExitCode != 0)
            {
                return new PackageCheckResponse(packageName, Models.SdkLanguage.Go, processResults);
            }

            // check for copyright headers
            var powershellOptions = new PowershellOptions(
                Path.Join(repoRoot, "eng", "scripts", "copyright-header-check.ps1"),
                ["-Packages", packagePath]
            );

            result = await powershellHelper.Run(powershellOptions, ct);
            processResults.Add(result);

            return new PackageCheckResponse(
                packageName,
                Models.SdkLanguage.Go,
                processResults);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(LintCode));
            return new PackageCheckResponse(packagePath, Models.SdkLanguage.Go, 1, "", $"{nameof(LintCode)} failed with an exception: {ex.Message}");
        }
    }

    public async Task<PackageCheckResponse> BuildProject(string packagePath, CancellationToken ct)
    {
        try
        {
            var packageName = await GetSubPath(packagePath, ct);
            var result = await processHelper.Run(new ProcessOptions(goUnix, goWin, ["build"], workingDirectory: packagePath), ct);
            return new PackageCheckResponse(packageName, Models.SdkLanguage.Go, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(BuildProject));
            return new PackageCheckResponse(packagePath, Models.SdkLanguage.Go, 1, "", $"{nameof(BuildProject)} failed with an exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the sub path, for the package, that most SDK scripts expect for -PackageName.
    /// </summary>
    /// <param name="packagePath">The full path to the package</param>
    /// <returns>The sub-path (ex: sdk/messaging/azservicebus)</returns>
    public async Task<string> GetSubPath(string packagePath, CancellationToken cancellationToken = default)
    {
        var gitRepoPath = await gitHelper.DiscoverRepoRootAsync(packagePath, cancellationToken);

        // ex: sdk/messaging/azservicebus/
        var relativePath = Path.GetRelativePath(gitRepoPath, packagePath);

        // Ensure forward slashes for Go package names and remove trailing slash
        var subPathNormalized = relativePath.Replace(Path.DirectorySeparatorChar, '/');

        return subPathNormalized.TrimEnd('/');
    }

    public override async Task<PackageCheckResponse> UpdateSnippets(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new PackageCheckResponse());
    }

    public override async Task<PackageCheckResponse> ValidateChangelog(string packagePath, bool fixCheckErrors = false, CancellationToken cancellationToken = default)
    {
        var packageSubPath = await this.GetSubPath(packagePath, cancellationToken);
        return await commonValidationHelpers.ValidateChangelog(packageSubPath, packagePath, fixCheckErrors, cancellationToken);
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        try
        {
            var result = await processHelper.Run(new ProcessOptions(goUnix, goWin, ["test", "-v", "-timeout", "1h", "./..."], workingDirectory: packagePath), ct);
            return new TestRunResponse(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{MethodName} failed with an exception", nameof(RunAllTests));
            return new TestRunResponse(1, null, $"Running Go tests failed: {ex.Message}");
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
    [GeneratedRegex(@"^module\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex GoModModuleLineRegex();
}

