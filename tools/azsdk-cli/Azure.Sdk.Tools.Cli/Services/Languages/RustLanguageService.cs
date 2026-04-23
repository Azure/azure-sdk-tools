// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific service for Rust packages. Since the Rust SDK repo does not have a
/// swagger_to_sdk_config.json, the build script path is hardcoded to eng/scripts/Build-Crates.ps1.
/// </summary>
public sealed class RustLanguageService : LanguageService
{
    private const string BuildScriptRelativePath = "eng/scripts/Build-Crates.ps1";
    private const string PackScriptRelativePath = "eng/scripts/Pack-Crates.ps1";

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
    /// Resolves package information for a Rust crate using <c>cargo read-manifest</c>,
    /// matching the approach used by Language-Settings.ps1 in the azure-sdk-for-rust repo.
    /// Extracts name, version, service directory, and SDK type.
    /// </summary>
    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        var fullPath = string.Empty;
        var repoRoot = string.Empty;
        var relativePath = string.Empty;
        var directoryPath = string.Empty;

        try
        {
            fullPath = RealPath.GetRealPath(packagePath);
            repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
            var sdkRoot = Path.Combine(repoRoot, "sdk");
            relativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar);
            directoryPath = $"sdk/{relativePath}";

            logger.LogDebug("Resolving Rust package info for path: {packagePath}", packagePath);

            var cargoTomlPath = Path.Combine(fullPath, "Cargo.toml");
            if (!File.Exists(cargoTomlPath))
            {
                logger.LogDebug("No Cargo.toml found at {CargoTomlPath}", cargoTomlPath);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            // Use 'cargo metadata' to extract package metadata.
            // cargo metadata --no-deps returns a packages array
            // that we filter by matching the directory name.
            var expectedCrateName = Path.GetFileName(fullPath);
            var cargoResult = await processHelper.Run(new ProcessOptions(
                command: "cargo",
                args: ["metadata", "--format-version", "1", "--no-deps", "--manifest-path", cargoTomlPath],
                logOutputStream: false,
                workingDirectory: fullPath,
                timeout: TimeSpan.FromSeconds(30)
            ), ct);

            if (cargoResult.ExitCode != 0)
            {
                logger.LogDebug("cargo metadata failed with exit code {ExitCode} for {Path}: {Output}",
                    cargoResult.ExitCode, cargoTomlPath, cargoResult.Output);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            string? packageName = null;
            string? packageVersion = null;

            using (var jsonDoc = JsonDocument.Parse(cargoResult.Stdout))
            {
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("packages", out var packagesElement) && packagesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pkg in packagesElement.EnumerateArray())
                    {
                        var name = pkg.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (string.Equals(name, expectedCrateName, StringComparison.OrdinalIgnoreCase))
                        {
                            packageName = name;
                            packageVersion = pkg.TryGetProperty("version", out var v) ? v.GetString() : null;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(packageName))
            {
                logger.LogDebug("Unable to find package matching '{ExpectedName}' in cargo metadata output for {Path}",
                    expectedCrateName, cargoTomlPath);
                return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
            }

            // Extract service directory from path: sdk/<serviceDir>/<package>
            var normalizedRelative = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            var serviceDirectory = GetServiceDirectory(normalizedRelative);

            // Determine SDK type: "mgmt" if name contains "azure_resourcemanager_", otherwise "client"
            var sdkType = packageName.Contains("azure_resourcemanager_", StringComparison.OrdinalIgnoreCase) ? "mgmt" : "client";

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

            logger.LogDebug("Resolved Rust package: {Package}", $"{model.PackageName ?? "(unknown)"}@{model.PackageVersion ?? "(unknown)"}");
            return model;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Exception thrown when trying to get package properties for {Path}", packagePath);
            return CreateEmptyPackageInfo(fullPath, repoRoot, relativePath);
        }
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
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo)> BuildAsync(
        string packagePath, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Building Rust SDK for project path: {PackagePath}", packagePath);
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return (false, "Package path is required and cannot be empty.", null);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPath))
            {
                return (false, $"Package full path does not exist: {fullPath}, input package path: {packagePath}.", null);
            }

            packagePath = fullPath;
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
                ["-ManifestDir", packagePath],
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

    /// <summary>
    /// Packs Rust crates by executing the Pack-Crates.ps1 script from the SDK repo.
    /// </summary>
    public override async Task<(bool Success, string? ErrorMessage, PackageInfo? PackageInfo, string? ArtifactPath)> PackAsync(
        string packagePath, string? outputPath = null, int timeoutMinutes = 30, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Packing Rust crate at: {PackagePath}", packagePath);

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return (false, "Package path is required and cannot be empty.", null, null);
            }

            string fullPath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPath))
            {
                return (false, $"Package path does not exist: {fullPath}, input package path: {packagePath}.", null, null);
            }

            packagePath = fullPath;
            string sdkRepoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return (false, $"Failed to discover local sdk repo with project-path: {packagePath}.", null, null);
            }

            PackageInfo? packageInfo = await GetPackageInfo(packagePath, ct);
            var scriptPath = Path.Combine(sdkRepoRoot, PackScriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                return (false, $"Pack script not found at: {scriptPath}.", packageInfo, null);
            }

            logger.LogDebug("Executing Rust pack script: {ScriptPath} for package: {PackageName}", scriptPath, packageInfo?.PackageName ?? "(unknown)");

            var args = new List<string> { "-ManifestDir", packagePath, "-NoVerify" };
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                args.AddRange(["-OutputPath", outputPath]);
            }

            var options = new PowershellOptions(
                scriptPath,
                args.ToArray(),
                logOutputStream: true,
                workingDirectory: sdkRepoRoot,
                timeout: TimeSpan.FromMinutes(timeoutMinutes)
            );

            var result = await powershellHelper.Run(options, ct);
            var trimmedOutput = (result.Output ?? string.Empty).Trim();

            if (result.ExitCode != 0)
            {
                var errorMessage = $"Pack failed with exit code {result.ExitCode}. Output:\n{trimmedOutput}";
                logger.LogDebug("Pack failed: {ErrorMessage}", errorMessage);
                return (false, errorMessage, packageInfo, null);
            }

            var artifactPath = ResolveArtifactPath(sdkRepoRoot, packageInfo, outputPath);
            if (artifactPath == null)
            {
                logger.LogDebug("Could not resolve artifact path. Package name: {Name}, version: {Version}",
                    packageInfo?.PackageName ?? "(null)", packageInfo?.PackageVersion ?? "(null)");
            }
            logger.LogDebug("Pack completed successfully. Artifact: {ArtifactPath}", artifactPath ?? "(unknown)");
            return (true, null, packageInfo, artifactPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while packing Rust crate");
            return (false, $"An error occurred: {ex.Message}", null, null);
        }
    }

    /// <summary>
    /// Resolves the path to the generated .crate artifact after a successful pack.
    /// When an output path is specified, the script copies artifacts to &lt;outputPath&gt;/&lt;name&gt;/&lt;name&gt;-&lt;version&gt;.crate.
    /// Otherwise, cargo places them at target/package/&lt;name&gt;-&lt;version&gt;.crate.
    /// </summary>
    private static string? ResolveArtifactPath(string repoRoot, PackageInfo? packageInfo, string? outputPath)
    {
        var name = packageInfo?.PackageName;
        var version = packageInfo?.PackageVersion;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
        {
            return null;
        }

        var dir = !string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(outputPath, name)
            : Path.Combine(repoRoot, "target", "package");

        var artifactPath = Path.Combine(dir, $"{name}-{version}.crate");
        return File.Exists(artifactPath) ? artifactPath : null;
    }
}
