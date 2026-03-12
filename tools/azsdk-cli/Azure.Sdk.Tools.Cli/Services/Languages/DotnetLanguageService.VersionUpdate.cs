// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// .NET-specific implementation of version file updates.
/// Follows the logic implemented in SetPackageVersion from azure-sdk-for-net eng/scripts/Language-Settings.ps1,
/// which delegates to Update-PkgVersion.ps1.
/// </summary>
public sealed partial class DotnetLanguageService : LanguageService
{
    private static readonly TimeSpan VersionScriptTimeout = TimeSpan.FromMinutes(2);

    // Regex to match <Version>...</Version> in .csproj XML
    private static readonly Regex CsprojVersionRegex = new(
        @"(<Version>)([^<]+)(</Version>)",
        RegexOptions.Compiled);

    /// <summary>
    /// Updates the package version in .NET-specific files (.csproj).
    /// Attempts to use the repo's Update-PkgVersion.ps1 script first, falls back to direct .csproj update.
    /// </summary>
    protected override async Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType, CancellationToken ct)
    {
        logger.LogInformation("Updating .NET package version to {Version} in {PackagePath}", version, packagePath);

        try
        {
            // Find .csproj file in src/ directory
            if (!TryGetCsprojPath(packagePath, out var csprojPath))
            {
                return PackageOperationResponse.CreateFailure(
                    $"No .csproj file found in {Path.Combine(packagePath, "src")}",
                    nextSteps: ["Ensure the package path contains a valid .NET project with a .csproj file in the src/ directory"]);
            }

            // Try using the repo's Update-PkgVersion.ps1 script (follows SetPackageVersion pattern)
            var scriptResult = await TryUpdateVersionUsingScriptAsync(packagePath, version, releaseType, ct);

            if (scriptResult.ScriptAvailable && scriptResult.Success)
            {
                return PackageOperationResponse.CreateSuccess(
                    $"Version updated to {version} via Update-PkgVersion.ps1.");
            }

            if (scriptResult.ScriptAvailable && !scriptResult.Success)
            {
                // Script was found but failed - report the error
                return PackageOperationResponse.CreateFailure(
                    scriptResult.Message ?? "Update-PkgVersion.ps1 failed.",
                    nextSteps:
                    [
                        "Check that pwsh (PowerShell) is installed and available",
                        "Ensure eng/scripts/Update-PkgVersion.ps1 exists in the repo",
                        "Run the script manually to verify: pwsh eng/scripts/Update-PkgVersion.ps1 -ServiceDirectory <service> -PackageName <name> -NewVersionString <version>",
                        "Check the running logs for details about the error"
                    ]);
            }

            // Script not available - fall back to direct .csproj update
            logger.LogInformation("Update-PkgVersion.ps1 not found, falling back to direct .csproj update");
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
    /// Attempts to run Update-PkgVersion.ps1 from the azure-sdk-for-net repo.
    /// This follows the pattern of SetPackageVersion in Language-Settings.ps1.
    /// </summary>
    private async Task<VersionScriptResult> TryUpdateVersionUsingScriptAsync(
        string packagePath, string version, string? releaseType, CancellationToken ct)
    {
        string repoRoot;
        try
        {
            repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to discover repo root for {PackagePath}; script update skipped", packagePath);
            return VersionScriptResult.NotAvailable("Failed to discover repository root.");
        }

        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return VersionScriptResult.NotAvailable("Repository root was not found.");
        }

        var updatePkgVersionScript = Path.Combine(repoRoot, "eng", "scripts", "Update-PkgVersion.ps1");
        if (!File.Exists(updatePkgVersionScript))
        {
            logger.LogDebug("Update-PkgVersion.ps1 not found at {ScriptPath}", updatePkgVersionScript);
            return VersionScriptResult.NotAvailable("Update-PkgVersion.ps1 was not found under eng/scripts.");
        }

        // Determine service directory and package name from the package path
        // .NET SDK packages follow the pattern: sdk/<service>/<PackageName>/
        var relativePath = Path.GetRelativePath(repoRoot, packagePath).Replace('\\', '/');
        var parts = relativePath.Split('/');

        if (parts.Length < 3 || !parts[0].Equals("sdk", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Package path {PackagePath} does not follow expected sdk/<service>/<package> layout", packagePath);
            return VersionScriptResult.NotAvailable(
                $"Package path does not follow expected sdk/<service>/<package> layout: {relativePath}");
        }

        var serviceDirectory = parts[1];
        var packageName = parts[2];

        logger.LogInformation("Running Update-PkgVersion.ps1 for {PackageName} in service {ServiceDirectory}",
            packageName, serviceDirectory);

        var scriptArgs = new List<string>
        {
            "-ServiceDirectory", serviceDirectory,
            "-PackageName", packageName,
            "-NewVersionString", version
        };

        // Add ReplaceLatestEntryTitle flag (default behavior in SetPackageVersion)
        scriptArgs.AddRange(["-ReplaceLatestEntryTitle", "$true"]);

        var result = await powershellHelper.Run(new PowershellOptions(
            scriptPath: updatePkgVersionScript,
            args: scriptArgs.ToArray(),
            workingDirectory: repoRoot,
            timeout: VersionScriptTimeout), ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("Update-PkgVersion.ps1 failed with exit code {ExitCode}: {Output}",
                result.ExitCode, result.Output);
            return VersionScriptResult.Failed($"Update-PkgVersion.ps1 failed (exit code {result.ExitCode}): {result.Output}");
        }

        logger.LogInformation("Update-PkgVersion.ps1 completed successfully for {PackageName}", packageName);
        return VersionScriptResult.Succeeded($"Version updated to {version} via Update-PkgVersion.ps1 for {packageName}.");
    }

    /// <summary>
    /// Falls back to directly updating the Version element in the .csproj file.
    /// This is used when the repo's versioning script is not available.
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
                    "Note: Direct .csproj update was used because Update-PkgVersion.ps1 was not found",
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

    private sealed record VersionScriptResult(bool ScriptAvailable, bool Success, string? Message)
    {
        public static VersionScriptResult NotAvailable(string message) => new(false, false, message);
        public static VersionScriptResult Failed(string message) => new(true, false, message);
        public static VersionScriptResult Succeeded(string message) => new(true, true, message);
    }
}
