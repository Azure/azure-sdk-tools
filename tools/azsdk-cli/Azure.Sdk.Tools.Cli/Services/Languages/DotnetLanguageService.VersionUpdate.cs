// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// .NET-specific implementation of version file updates.
/// Handles changelog updates (via base class) and .csproj version updates directly in C#,
/// avoiding a PowerShell script dependency.
/// </summary>
public sealed partial class DotnetLanguageService : LanguageService
{
    // Regex to match <Version>...</Version> in .csproj XML
    private static readonly Regex CsprojVersionRegex = new(
        @"(<Version>)([^<]+)(</Version>)",
        RegexOptions.Compiled);

    /// <summary>
    /// Overrides the base version update to add .NET-specific validation that the
    /// release type matches the version format (e.g., stable versions must not have
    /// a pre-release suffix). Delegates to the base class for changelog and version
    /// file updates.
    /// </summary>
    public override async Task<PackageOperationResponse> UpdateVersionAsync(
        string packagePath, string? releaseType, string? version, string? releaseDate, CancellationToken ct)
    {
        var packageInfo = await GetPackageInfo(packagePath, ct);

        var targetVersion = version;
        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            targetVersion = packageInfo?.PackageVersion;
            if (string.IsNullOrWhiteSpace(targetVersion))
            {
                return PackageOperationResponse.CreateFailure(
                    "Version is required. Unable to determine the current package version.",
                    packageInfo: packageInfo,
                    nextSteps: ["Provide the version parameter explicitly"]);
            }
        }

        // Default release date to today if not provided
        if (string.IsNullOrWhiteSpace(releaseDate))
        {
            releaseDate = DateTime.Now.ToString("yyyy-MM-dd");
        }

        // Validate release type vs version format.
        // Stable (GA) releases are guarded — they require the user to explicitly pass
        // --release-type stable. This prevents accidental GA releases when the default
        // release type is "beta" but the version happens to be stable.
        var isPrerelease = targetVersion.Contains('-');
        if (!isPrerelease)
        {
            if (string.IsNullOrWhiteSpace(releaseType) ||
                !releaseType.Equals("stable", StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResponse.CreateFailure(
                    $"Version '{targetVersion}' is a stable (GA) release. Stable releases require explicit confirmation.",
                    packageInfo: packageInfo,
                    nextSteps: [$"Pass --release-type stable to confirm this is a GA release"]);
            }
        }
        else if (!string.IsNullOrWhiteSpace(releaseType) &&
                 releaseType.Equals("stable", StringComparison.OrdinalIgnoreCase))
        {
            return PackageOperationResponse.CreateFailure(
                $"Release type 'stable' does not match pre-release version '{targetVersion}'. Stable versions must not contain a pre-release suffix.",
                packageInfo: packageInfo,
                nextSteps: [$"Use --release-type beta, or remove the pre-release suffix from the version (e.g., '{targetVersion.Split('-')[0]}')"]);
        }

        // Delegate to the base class which handles:
        // 1. Changelog entry title update (version + date) via ChangelogHelper.UpdateReleaseDate
        //    This always replaces the latest entry title, matching Prepare-Release.ps1 behavior,
        //    so version promotion (e.g., 12.28.0-beta.2 → 12.28.0) is handled automatically.
        // 2. Language-specific version file updates via UpdatePackageVersionInFilesAsync
        return await base.UpdateVersionAsync(packagePath, releaseType, targetVersion, releaseDate, ct);
    }

    /// <summary>
    /// Updates the package version in .NET-specific files (.csproj).
    /// </summary>
    protected override async Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType, CancellationToken ct)
    {
        logger.LogInformation("Updating .NET package version to {Version} in {PackagePath}", version, packagePath);

        try
        {
            if (!TryGetCsprojPath(packagePath, out var csprojPath))
            {
                return PackageOperationResponse.CreateFailure(
                    $"No .csproj file found in {Path.Combine(packagePath, "src")}",
                    nextSteps: ["Ensure the package path contains a valid .NET project with a .csproj file in the src/ directory"]);
            }

            return await UpdateCsprojVersionDirectlyAsync(csprojPath, version, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update .NET package version");
            return PackageOperationResponse.CreateFailure(
                $"Failed to update version: {ex.Message}",
                nextSteps: ["Check the .csproj file format", "Ensure the file is not locked by another process"]);
        }
    }

    /// <summary>
    /// Directly updates the Version element in the .csproj file.
    /// </summary>
    private async Task<PackageOperationResponse> UpdateCsprojVersionDirectlyAsync(
        string csprojPath, string version, CancellationToken ct)
    {
        logger.LogInformation("Updating version directly in {CsprojPath}", csprojPath);

        try
        {
            var content = await File.ReadAllTextAsync(csprojPath, ct);

            if (!CsprojVersionRegex.IsMatch(content))
            {
                logger.LogWarning("No <Version> element found in {CsprojPath}", csprojPath);
                return PackageOperationResponse.CreateFailure(
                    $"No <Version> element found in {Path.GetFileName(csprojPath)}.",
                    nextSteps:
                    [
                        "Ensure the .csproj file contains a <Version> element in a <PropertyGroup>",
                        "Add <Version>1.0.0-beta.1</Version> to the .csproj file manually"
                    ]);
            }

            var newContent = CsprojVersionRegex.Replace(content, $"${{1}}{version}${{3}}");

            if (newContent == content)
            {
                logger.LogInformation("Version in {CsprojPath} already set to {Version}; no change needed",
                    csprojPath, version);
                return PackageOperationResponse.CreateSuccess(
                    $"Version already set to {version} in {Path.GetFileName(csprojPath)}; no change needed.");
            }

            await File.WriteAllTextAsync(csprojPath, newContent, ct);
            logger.LogInformation("Updated <Version> to {Version} in {CsprojPath}", version, csprojPath);

            return PackageOperationResponse.CreateSuccess(
                $"Updated version to {version} in {Path.GetFileName(csprojPath)}.",
                nextSteps:
                [
                    "Central Package Management (CPM) files were NOT updated - update eng/Packages.Data.props manually if needed",
                    "Review the changes and run validation checks"
                ]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating version in {CsprojPath}", csprojPath);
            return PackageOperationResponse.CreateFailure(
                $"Error updating {Path.GetFileName(csprojPath)}: {ex.Message}",
                nextSteps: ["Ensure the .csproj file is valid XML", "Ensure the file is not locked by another process"]);
        }
    }

    /// <summary>
    /// Tries to locate the .csproj file in the src/ subdirectory of the package path.
    /// </summary>
    private bool TryGetCsprojPath(string packagePath, out string csprojPath)
    {
        var srcDir = Path.Combine(packagePath, "src");
        csprojPath = string.Empty;

        if (!Directory.Exists(srcDir))
        {
            logger.LogDebug("src/ directory not found at {PackagePath}", packagePath);
            return false;
        }

        var csprojFiles = Directory.GetFiles(srcDir, "*.csproj");
        if (csprojFiles.Length == 0)
        {
            logger.LogDebug("No .csproj files found in {SrcDir}", srcDir);
            return false;
        }

        csprojPath = csprojFiles[0];
        if (csprojFiles.Length > 1)
        {
            logger.LogWarning("Multiple .csproj files found in {SrcDir}, using {CsprojPath}", srcDir, csprojPath);
        }

        return true;
    }

}
