// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific helper for Go packages. Provides structural package info plus lazy accessors
/// for samples directory, file extension, and version parsing.
/// </summary>
public partial class GoLanguageService : LanguageService
{
    private readonly INpxHelper npxHelper;

    public GoLanguageService(
        IProcessHelper processHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers)
    {
        this.npxHelper = npxHelper;
        base.processHelper = processHelper;
        base.gitHelper = gitHelper;
        base.logger = logger;
        base.commonValidationHelpers = commonValidationHelpers;
    }

    private readonly string compilerName = "go";
    private readonly string compilerNameWindows = "go.exe";
    private readonly string formatterName = "goimports";
    private readonly string formatterNameWindows = "gofmt.exe";
    private readonly string linterName = "golangci-lint";
    private readonly string linterNameWindows = "golangci-lint.exe";
    public override SdkLanguage Language { get; } = SdkLanguage.Go;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Go package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for Go package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for Go package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.Go,
            SamplesDirectory = fullPath
        };
        
        logger.LogDebug("Resolved Go package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        string? packageName = null;
        string? packageVersion = null;

        // Extract package name from go.mod
        var goModFile = Path.Combine(packagePath, "go.mod");
        if (File.Exists(goModFile))
        {
            try
            {
                logger.LogTrace("Reading go.mod from {goModFile}", goModFile);
                var goModContent = await File.ReadAllTextAsync(goModFile, ct);
                // Match: module github.com/Azure/azure-sdk-for-go/sdk/storage/azblob
                var match = GoModModuleDeclarationRegex().Match(goModContent);
                if (match.Success)
                {
                    var modulePath = match.Groups[1].Value.Trim();
                    // Extract the last segment as the package name
                    var segments = modulePath.Split('/');
                    packageName = segments.Length > 0 ? segments[^1] : modulePath;
                    logger.LogTrace("Found Go package name from go.mod: {packageName} (module: {modulePath})", packageName, modulePath);
                }
                else
                {
                    logger.LogTrace("No module declaration found in go.mod");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error reading go.mod from {goModFile}", goModFile);
            }
        }
        else
        {
            logger.LogWarning("No go.mod file found at {goModFile}", goModFile);
        }

        // Extract version from version.go
        var versionFile = Path.Combine(packagePath, "version.go");
        if (File.Exists(versionFile))
        {
            try
            {
                logger.LogTrace("Reading version.go from {versionFile}", versionFile);
                var content = await File.ReadAllTextAsync(versionFile, ct);
                // Match: Version = "v1.2.3" OR version = "v1.2.3"
                var match = GoVersionDeclarationRegex().Match(content);
                if (match.Success)
                {
                    packageVersion = match.Groups[1].Value.Trim();
                    logger.LogTrace("Found Go version from simple declaration: {packageVersion}", packageVersion);
                }
                else
                {
                    logger.LogTrace("No version declaration found in version.go");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error reading version.go from {versionFile}", versionFile);
            }
        }
        else
        {
            logger.LogTrace("No version.go file found at {versionFile}", versionFile);
        }

        return (packageName, packageVersion);
    }

    private static (string RepoRoot, string RelativePath, string FullPath) Parse(IGitHelper gitHelper, string realPackagePath)
    {
        var full = RealPath.GetRealPath(realPackagePath);
        var repoRoot = gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }

    /// <summary>
    /// Matches go.mod module declarations like "module github.com/Azure/azure-sdk-for-go/sdk/storage/azblob"
    /// </summary>
    [GeneratedRegex(@"^\s*module\s+(.+?)(\s|$)", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex GoModModuleDeclarationRegex();

    /// <summary>
    /// Matches Go version declarations like 'Version = "v1.2.3"' or 'version = "v1.2.3"'
    /// </summary>
    [GeneratedRegex(@"(?m)^\s*[Vv]ersion\s*=\s*""([^"" ]+)""", RegexOptions.Compiled)]
    private static partial Regex GoVersionDeclarationRegex();

    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return categories.TryGetValue("go", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();
    }
}
