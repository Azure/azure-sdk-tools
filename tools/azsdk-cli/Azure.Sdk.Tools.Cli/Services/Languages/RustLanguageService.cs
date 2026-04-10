// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific service for Rust packages. Since the Rust SDK repo does not have a
/// swagger_to_sdk_config.json, the build script path is hardcoded to eng/scripts/build-sdk.ps1.
/// </summary>
public sealed partial class RustLanguageService : LanguageService
{
    private const string BuildScriptRelativePath = "eng/scripts/build-sdk.ps1";

    public RustLanguageService(
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

    private readonly IPowershellHelper powershellHelper;

    public override SdkLanguage Language { get; } = SdkLanguage.Rust;

    /// <summary>
    /// Rust packages are identified by Cargo.toml files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["Cargo.toml"];

    /// <summary>
    /// Resolves package information for a Rust crate by parsing its Cargo.toml manifest.
    /// Extracts name, version, service directory, and SDK type following the same conventions
    /// as the PowerShell Language-Settings.ps1 script.
    /// </summary>
    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        var fullPath = RealPath.GetRealPath(packagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar);
        var directoryPath = $"sdk/{relativePath}";

        try
        {
            logger.LogDebug("Resolving Rust package info for path: {packagePath}", packagePath);

            var cargoTomlPath = Path.Combine(fullPath, "Cargo.toml");
            if (!File.Exists(cargoTomlPath))
            {
                logger.LogDebug("No Cargo.toml found at {CargoTomlPath}", cargoTomlPath);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            var cargoContent = await File.ReadAllTextAsync(cargoTomlPath, ct);
            var packageName = ExtractCargoField(cargoContent, "name");
            var packageVersion = ExtractCargoField(cargoContent, "version");

            if (string.IsNullOrEmpty(packageName))
            {
                logger.LogDebug("Unable to extract package name from Cargo.toml at {Path}", cargoTomlPath);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            // Extract service directory from path: sdk/<serviceDir>/<package>
            var normalizedRelative = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            var serviceDirectory = GetServiceDirectory(normalizedRelative);

            // Determine SDK type: "mgmt" if name contains "mgmt", otherwise "client"
            var sdkType = packageName.Contains("mgmt", StringComparison.OrdinalIgnoreCase) ? "mgmt" : "client";

            var readmePath = Path.Combine(fullPath, "README.md");
            var changelogPath = Path.Combine(fullPath, "CHANGELOG.md");
            var readmeRelative = File.Exists(readmePath) ? $"sdk/{normalizedRelative}/README.md" : string.Empty;
            var changelogRelative = File.Exists(changelogPath) ? $"sdk/{normalizedRelative}/CHANGELOG.md" : string.Empty;
            var releaseStatus = File.Exists(changelogPath)
                ? await changelogHelper.GetReleaseStatus(changelogPath, ct)
                : string.Empty;

            var model = new PackageInfo
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = relativePath,
                PackageName = packageName,
                PackageVersion = packageVersion,
                ServiceName = serviceDirectory ?? string.Empty,
                SdkTypeString = sdkType,
                Language = SdkLanguage.Rust,
                SamplesDirectory = fullPath,
                DirectoryPath = directoryPath,
                ServiceDirectory = serviceDirectory,
                ReadMePath = readmeRelative,
                ChangeLogPath = changelogRelative,
                IsNewSdk = true,
                ArtifactName = packageName,
                ReleaseStatus = releaseStatus,
                SpecProjectPath = GetSpecProjectPath(fullPath)
            };

            logger.LogDebug("Resolved Rust package: {packageName} v{packageVersion}", model.PackageName ?? "(unknown)", model.PackageVersion ?? "(unknown)");
            return model;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Exception thrown when trying to get package properties for {Path}", packagePath);
            return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
        }
    }

    /// <summary>
    /// Extracts a field value from the [package] section of a Cargo.toml file.
    /// Handles workspace-inherited values (e.g., "version.workspace = true") by returning null.
    /// </summary>
    internal static string? ExtractCargoField(string cargoContent, string fieldName)
    {
        // Match: fieldName = "value" (in [package] section, before next section)
        var packageSectionMatch = CargoPackageSectionRegex().Match(cargoContent);
        if (!packageSectionMatch.Success)
        {
            return null;
        }

        var packageSection = packageSectionMatch.Groups["content"].Value;
        var fieldPattern = new Regex($@"^{Regex.Escape(fieldName)}\s*=\s*""(?<value>[^""]+)""", RegexOptions.Multiline);
        var fieldMatch = fieldPattern.Match(packageSection);
        return fieldMatch.Success ? fieldMatch.Groups["value"].Value : null;
    }

    /// <summary>
    /// Extracts the service directory from a relative path under sdk/.
    /// For path "core/azure_core" returns "core".
    /// </summary>
    private static string? GetServiceDirectory(string normalizedRelativePath)
    {
        var parts = normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 1 ? parts[0] : null;
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
            ServiceName = string.Empty,
            SdkType = SdkType.Unknown,
            Language = SdkLanguage.Rust,
            SamplesDirectory = fullPath,
            DirectoryPath = $"sdk/{relativePath}",
            ReadMePath = string.Empty,
            ChangeLogPath = string.Empty,
            ReleaseStatus = string.Empty,
            SpecProjectPath = GetSpecProjectPath(fullPath)
        };
    }

    /// <summary>
    /// Builds Rust SDK code by executing the hardcoded build script at eng/scripts/build-sdk.ps1.
    /// Unlike other languages, Rust does not use swagger_to_sdk_config.json for build configuration.
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(
        string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Building Rust SDK for project path: {PackagePath}", packagePath);

            string sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return (false, $"Failed to discover local sdk repo with project-path: {packagePath}.", null);
            }

            PackageInfo? packageInfo = await GetPackageInfo(packagePath, ct);

            var scriptPath = Path.Combine(sdkRepoRoot, BuildScriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                return (false, $"Build script not found at: {scriptPath}.", packageInfo);
            }

            logger.LogDebug("Executing Rust build script: {ScriptPath}", scriptPath);

            var options = new PowershellOptions(
                scriptPath,
                ["-PackagePath", packagePath],
                logOutputStream: true,
                workingDirectory: sdkRepoRoot,
                timeout: TimeSpan.FromMinutes(timeoutMinutes)
            );

            var result = await powershellHelper.Run(options, ct);
            var trimmedOutput = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                var errorMessage = $"Build failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}";
                logger.LogDebug("Build failed: {ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo);
            }

            logger.LogDebug("Build completed successfully.");
            return (true, null, packageInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while building Rust SDK code");
            return (false, $"An error occurred: {ex.Message}", null);
        }
    }

    // Matches the [package] section content up to the next section header or end of file
    [GeneratedRegex(@"\[package\]\s*\n(?<content>(?:(?!\n\[).)*)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CargoPackageSectionRegex();
}
