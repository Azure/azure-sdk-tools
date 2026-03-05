// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Python-specific implementation of version file updates.
/// </summary>
public partial class PythonLanguageService : LanguageService
{
    // Regex patterns for matching version assignment in _version.py and the development status classifier in setup.py/pyproject.toml
    private static readonly Regex VersionRegex = new(
        @"^VERSION\s*=\s*['""]([^'""]*)['""]",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex DevStatusRegex = new(
        @"(classifiers\s*=\s*\[(\s)*)(['""]Development Status :: [^'""\r\n]*['""])",
        RegexOptions.Compiled);

    // Directories to skip when searching for version files (matches Python SDK's EXCLUDE set)
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "venv", "__pycache__", "tests", "test", "generated_samples", "generated_tests",
        "samples", "swagger", "stress", "docs", "doc", "local", "scripts", "images", ".tox"
    };

    /// <summary>
    /// Updates version.py (_version.py) and the development status classifier in setup.py or pyproject.toml.
    /// </summary>
    protected override async Task<PackageOperationResponse> UpdatePackageVersionInFilesAsync(
        string packagePath, string version, string? releaseType, CancellationToken ct)
    {
        logger.LogInformation("Updating Python package version files at {PackagePath} to version {Version}",
            packagePath, version);

        var errors = new List<string>();
        bool versionFileUpdated = false;

        // Step 1: Find and update _version.py or version.py
        var versionFilePath = FindVersionPy(packagePath);
        if (versionFilePath != null)
        {
            try
            {
                var content = await File.ReadAllTextAsync(versionFilePath, ct);
                var newContent = VersionRegex.Replace(content, $@"VERSION = ""{version}""");

                if (newContent != content)
                {
                    await File.WriteAllTextAsync(versionFilePath, newContent, ct);
                    logger.LogInformation("Updated VERSION in {VersionFile}", versionFilePath);
                    versionFileUpdated = true;
                }
                else
                {
                    logger.LogWarning("VERSION pattern not found or already set in {VersionFile}", versionFilePath);
                    errors.Add($"Could not find VERSION pattern in {Path.GetFileName(versionFilePath)}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating version file {VersionFile}", versionFilePath);
                errors.Add($"Error updating {Path.GetFileName(versionFilePath)}: {ex.Message}");
            }
        }
        else
        {
            logger.LogWarning("No _version.py or version.py found in {PackagePath}", packagePath);
            errors.Add("No _version.py or version.py found in package directory");
        }

        // Step 2: Find and update development status classifier in setup.py or pyproject.toml
        var setupFilePath = FindSetupFile(packagePath);
        if (setupFilePath != null)
        {
            try
            {
                var classification = IsPreRelease(version, releaseType)
                    ? "Development Status :: 4 - Beta"
                    : "Development Status :: 5 - Production/Stable";

                var content = await File.ReadAllTextAsync(setupFilePath, ct);
                var newContent = DevStatusRegex.Replace(
                    content,
                    m => $"{m.Groups[1].Value}\"{classification}\"");

                if (newContent != content)
                {
                    await File.WriteAllTextAsync(setupFilePath, newContent, ct);
                    logger.LogInformation("Updated development status classifier in {SetupFile} to '{Classification}'",
                        Path.GetFileName(setupFilePath), classification);
                }
                else
                {
                    logger.LogDebug("Development status classifier not found or unchanged in {SetupFile}",
                        Path.GetFileName(setupFilePath));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating setup file {SetupFilePath}", setupFilePath);
                errors.Add($"Error updating {Path.GetFileName(setupFilePath)}: {ex.Message}");
            }
        }
        else
        {
            logger.LogDebug("No setup.py or pyproject.toml found at package root {PackagePath}", packagePath);
        }

        if (errors.Count > 0 && !versionFileUpdated)
        {
            return PackageOperationResponse.CreateFailure(
                string.Join("; ", errors),
                nextSteps: ["Manually update _version.py or version.py with the new version"]);
        }

        if (errors.Count > 0)
        {
            logger.LogWarning("Version files partially updated. Errors: {Errors}", string.Join("; ", errors));
        }

        return PackageOperationResponse.CreateSuccess(
            $"Updated Python package version files to {version}.");
    }

    /// <summary>
    /// Finds _version.py or version.py in the package directory, recursively.
    /// Follows the same logic as the Python SDK's get_version_py function.
    /// </summary>
    private string? FindVersionPy(string packagePath)
    {
        var queue = new Queue<string>();
        queue.Enqueue(packagePath);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();

            // Check for _version.py first (preferred), then version.py (legacy)
            var versionPy = Path.Combine(dir, "_version.py");
            if (File.Exists(versionPy))
            {
                return versionPy;
            }

            var oldVersionPy = Path.Combine(dir, "version.py");
            if (File.Exists(oldVersionPy))
            {
                return oldVersionPy;
            }

            try
            {
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (dirName != null
                        && !dirName.StartsWith('_')
                        && !dirName.StartsWith('.')
                        && dirName != "build"
                        && !dirName.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase)
                        && !ExcludedDirs.Contains(dirName))
                    {
                        queue.Enqueue(subDir);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error listing subdirectories of {Dir}", dir);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds setup.py or pyproject.toml at the package root.
    /// </summary>
    private static string? FindSetupFile(string packagePath)
    {
        var setupPy = Path.Combine(packagePath, "setup.py");
        if (File.Exists(setupPy))
        {
            return setupPy;
        }

        var pyprojectToml = Path.Combine(packagePath, "pyproject.toml");
        if (File.Exists(pyprojectToml))
        {
            return pyprojectToml;
        }

        return null;
    }

    /// <summary>
    /// Determines if a Python package version is a prerelease.
    /// Checks the releaseType parameter first, then falls back to inspecting the version string
    /// for common Python pre-release patterns (alpha, beta, rc, dev).
    /// </summary>
    private static bool IsPreRelease(string version, string? releaseType)
    {
        if (releaseType?.Equals("beta", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        // Detect Python pre-release version patterns: e.g. 1.0.0b2, 1.0.0a1, 1.0.0rc1, 1.0.0.dev20230101
        return Regex.IsMatch(version, @"\d(a|b|rc|\.dev)\d");
    }
}
