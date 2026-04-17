// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Go-specific implementation of version file updates.
/// Mirrors the logic in azure-sdk-for-go/eng/scripts/Update-ModuleVersion.ps1:
/// finds the version file (*constants.go or *version.go), replaces the old version
/// string with the new one, and writes the file back.
/// </summary>
public partial class GoLanguageService : LanguageService
{
    /// <summary>
    /// Updates the version constant in Go version files (constants.go or version.go).
    /// The version in Go files uses a "v" prefix (e.g., const Version = "v1.2.3").
    /// This method replaces the semver portion while preserving the prefix.
    /// </summary>
    protected override async Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType, CancellationToken ct)
    {
        // Normalize: strip leading 'v' if present — the file stores "vX.Y.Z" but the
        // captured version group and replacement value use the bare semver string
        var normalizedVersion = version.TrimStart('v');

        logger.LogInformation("Updating Go package version files at {PackagePath} to version {Version}",
            packagePath, normalizedVersion);

        // Find the version file and current version using the existing discovery logic
        var (currentVersion, versionFile) = await GetGoModuleVersionInfoAsync(packagePath, ct);

        if (versionFile == null || currentVersion == null)
        {
            return PackageOperationResponse.CreateFailure(
                "No Go version file found. Expected a .go file containing 'constant' or 'version' in its name " +
                "with a version pattern like: const Version = \"vX.Y.Z\"",
                nextSteps: ["Create a version.go or constants.go file with a version constant"]);
        }

        try
        {
            var content = await File.ReadAllTextAsync(versionFile, ct);

            // Use regex to precisely target only the version in the assignment line,
            // avoiding accidental replacement of matching strings in comments or other constants
            var match = GoVersionLineRegex().Match(content);
            if (!match.Success)
            {
                return PackageOperationResponse.CreateFailure(
                    $"Version pattern not found in {Path.GetFileName(versionFile)}",
                    nextSteps: [$"Manually update the version in {Path.GetFileName(versionFile)}"]);
            }

            var versionGroup = match.Groups["version"];
            var newContent = string.Concat(
                content.AsSpan(0, versionGroup.Index),
                normalizedVersion,
                content.AsSpan(versionGroup.Index + versionGroup.Length));

            if (newContent == content)
            {
                logger.LogInformation("Version already set to {Version} in {VersionFile}; no change needed",
                    normalizedVersion, versionFile);
                return PackageOperationResponse.CreateSuccess(
                    $"Version already set to {normalizedVersion} in {Path.GetFileName(versionFile)}.");
            }

            await File.WriteAllTextAsync(versionFile, newContent, ct);
            logger.LogInformation("Updated version from {OldVersion} to {NewVersion} in {VersionFile}",
                currentVersion, normalizedVersion, versionFile);

            return PackageOperationResponse.CreateSuccess(
                $"Updated Go package version to {normalizedVersion} in {Path.GetFileName(versionFile)}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating version file {VersionFile}", versionFile);
            return PackageOperationResponse.CreateFailure(
                $"Error updating {Path.GetFileName(versionFile)}: {ex.Message}",
                nextSteps: [$"Manually update the version in {Path.GetFileName(versionFile)}"]);
        }
    }
}
