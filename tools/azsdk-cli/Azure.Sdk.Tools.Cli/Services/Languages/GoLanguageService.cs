// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific helper for Go packages. Provides structural package info plus lazy accessors
/// for samples directory, file extension, and version parsing.
/// </summary>
public partial class GoLanguageService : LanguageService
{
    public GoLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    private readonly string goUnix = "go";
    private readonly string goWin = "go.exe";
    private readonly string gofmtUnix = "gofmt";
    private readonly string gofmtWin = "gofmt.exe";
    private readonly string golangciLintUnix = "golangci-lint";
    private readonly string golangciLintWin = "golangci-lint.exe";
    private readonly IPowershellHelper powershellHelper;

    // Known locations for Go customization files
    private const string CustomizationPathInternalGenerate = "internal/generate";
    private const string CustomizationPathTestdataGenerate = "testdata/generate";

    public override SdkLanguage Language { get; } = SdkLanguage.Go;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// Go packages are identified by go.mod files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["go.mod"];

    protected override void ApplyLanguageCiParameters(PackageInfo packageInfo)
    {
        var parameters = packageInfoHelper.GetLanguageCiParameters<GoCiPipelineYamlParameters>(packageInfo)
            ?? new GoCiPipelineYamlParameters();

        packageInfo.CiParameters.LicenseCheck = parameters.LicenseCheck;
        packageInfo.CiParameters.NonShipping = parameters.NonShipping;
        packageInfo.CiParameters.UsePipelineProxy = parameters.UsePipelineProxy;
        packageInfo.CiParameters.IsSdkLibrary = parameters.IsSdkLibrary;
    }

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        var fullPath = RealPath.GetRealPath(packagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar);
        var directoryPath = $"sdk/{relativePath}";

        try
        {
            logger.LogDebug("Resolving Go package info for path: {packagePath}", packagePath);
            var goModuleProperties = await TryGetGoModulePropertiesAsync(fullPath, repoRoot, ct);
            if (goModuleProperties is null)
            {
                logger.LogDebug("Unable to derive Go module properties for package path: {PackagePath}", packagePath);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            var readmePath = Path.Combine(fullPath, "README.md");
            var changelogPath = Path.Combine(fullPath, "CHANGELOG.md");
            var readmeRelative = File.Exists(readmePath) ? $"{goModuleProperties.Name}/README.md" : string.Empty;
            var changelogRelative = File.Exists(changelogPath) ? $"{goModuleProperties.Name}/CHANGELOG.md" : string.Empty;
            var releaseStatus = File.Exists(changelogPath)
                ? await changelogHelper.GetReleaseStatus(changelogPath, ct)
                : string.Empty;

            var model = new PackageInfo
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = relativePath,
                PackageName = goModuleProperties.Name,
                PackageVersion = goModuleProperties.Version,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                SdkTypeString = goModuleProperties.SdkType,
                Language = SdkLanguage.Go,
                SamplesDirectory = fullPath,
                DirectoryPath = directoryPath,
                ServiceDirectory = goModuleProperties.ServiceDirectory,
                ReadMePath = readmeRelative,
                ChangeLogPath = changelogRelative,
                IsNewSdk = goModuleProperties.IsNewSdk,
                ArtifactName = goModuleProperties.Name,
                ReleaseStatus = releaseStatus,
                SpecProjectPath = GetSpecProjectPath(fullPath)
            };

            logger.LogDebug("Resolved Go package: {packageName} v{packageVersion}", model.PackageName ?? "(unknown)", model.PackageVersion ?? "(unknown)");
            return model;
        }
        catch (Exception ex)
        {
            // NOTE: this method, via the LanguageService, cannot throw these exceptions, so we only log it.
            logger.LogDebug(ex, "Exception thrown when trying to get package properties for {Path}", packagePath);
            return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
        }
    }

    public override string? HasCustomizations(string packagePath, CancellationToken ct)
    {
        // Go customization files can live in different locations depending on the package.
        // Known locations include:
        //   - internal/generate (most common)
        //   - testdata/generate (e.g., azcertificates)
        // TODO: In the future, check tspconfig.yaml for "go-generate" directive for definitive detection.

        try
        {
            string[] knownLocations = [CustomizationPathInternalGenerate, CustomizationPathTestdataGenerate];

            foreach (var location in knownLocations)
            {
                var customizationPath = Path.Combine(packagePath, location);
                if (Directory.Exists(customizationPath))
                {
                    logger.LogDebug("Found Go customization directory at {CustomizationPath}", customizationPath);
                    return customizationPath;
                }
            }

            logger.LogDebug("No Go customization directory found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for Go customization files in {PackagePath}", packagePath);
            return null;
        }
    }

    private async Task<GoModuleProperties?> TryGetGoModulePropertiesAsync(string packagePath, string repoRoot, CancellationToken ct)
    {
        var relativePath = Path.GetRelativePath(repoRoot, packagePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Trim('/');

        if (relativePath.Contains("testdata", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("sdk/samples", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var modMatch = GoModulePathRegex().Match(relativePath);
        if (!modMatch.Success)
        {
            return null;
        }

        var modulePath = modMatch.Groups["modPath"].Value;
        var moduleName = modMatch.Groups["modName"].Value;
        var serviceDirectory = modulePath[(modulePath.IndexOf('/') + 1)..];
        var sdkType = GetSdkType(modulePath, moduleName);

        var (version, _) = await GetGoModuleVersionInfoAsync(packagePath, ct);
        if (string.IsNullOrEmpty(version) && !string.Equals(sdkType, "eng", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new GoModuleProperties(
            Name: modulePath,
            Version: version,
            ServiceDirectory: serviceDirectory,
            SdkType: sdkType,
            IsNewSdk: true);
    }

    private async Task<(string? Version, string? VersionFile)> GetGoModuleVersionInfoAsync(string packagePath, CancellationToken ct)
    {
        foreach (var goFile in Directory.EnumerateFiles(packagePath, "*.go", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(goFile);
            if (!fileName.Contains("constant", StringComparison.OrdinalIgnoreCase)
                && !fileName.Contains("version", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(goFile, ct);
            var versionMatch = GoVersionLineRegex().Match(content);
            if (versionMatch.Success)
            {
                return (versionMatch.Groups["version"].Value, goFile);
            }

            var badVersionMatch = GoVersionLineNoPrefixRegex().Match(content);
            if (badVersionMatch.Success)
            {
                var badVersion = badVersionMatch.Groups["badVersion"].Value;
                logger.LogError("Version in {VersionFile} should be 'v{ExpectedVersion}' not '{ActualVersion}'", goFile, badVersion, badVersion);
            }
        }

        logger.LogWarning("Unable to find version for {ModulePath}", packagePath);
        return (null, null);
    }

    private static string GetSdkType(string modulePath, string moduleName)
    {
        if (modulePath.Contains("eng/tools", StringComparison.OrdinalIgnoreCase))
        {
            return "eng";
        }

        if (moduleName.StartsWith("arm", StringComparison.OrdinalIgnoreCase)
            || modulePath.Contains("resourcemanager", StringComparison.OrdinalIgnoreCase))
        {
            return "mgmt";
        }

        return "client";
    }

    private static PackageInfo CreateEmptyPackageInfo(string fullPath, string repoRoot, string relativePath)
    {
        return new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = null,
            PackageVersion = null,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            SdkType = SdkType.Unknown,
            Language = SdkLanguage.Go,
            SamplesDirectory = fullPath,
            DirectoryPath = $"sdk/{relativePath}",
            ReadMePath = string.Empty,
            ChangeLogPath = string.Empty,
            ReleaseStatus = string.Empty,
            SpecProjectPath = GetSpecProjectPath(fullPath)
        };
    }

    private sealed record GoModuleProperties(
        string Name,
        string? Version,
        string ServiceDirectory,
        string SdkType,
        bool IsNewSdk);

    private const string SemVerPattern = @"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*)?(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?";

    [GeneratedRegex(@"^(?<modPath>(sdk|profile|eng)/(?<serviceDir>(.*?(?<serviceName>[^/]+)/)?(?<modName>[^/]+)))$", RegexOptions.CultureInvariant)]
    private static partial Regex GoModulePathRegex();

    [GeneratedRegex(".+\\s*=\\s*\".*v(?<version>" + SemVerPattern + ")\"", RegexOptions.CultureInvariant)]
    private static partial Regex GoVersionLineRegex();

    [GeneratedRegex(".+\\s*=\\s*\"(?<badVersion>" + SemVerPattern + ")\"", RegexOptions.CultureInvariant)]
    private static partial Regex GoVersionLineNoPrefixRegex();

    internal sealed class GoCiPipelineYamlParameters : CiPipelineYamlParametersBase
    {
        public bool? LicenseCheck { get; set; } = true;
        public bool? NonShipping { get; set; } = false;
        public bool? UsePipelineProxy { get; set; } = true;
        public bool? IsSdkLibrary { get; set; } = true;
    }
}
