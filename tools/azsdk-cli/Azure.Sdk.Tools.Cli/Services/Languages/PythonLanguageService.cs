// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

public sealed partial class PythonLanguageService : LanguageService
{
    private readonly INpxHelper npxHelper;

    public PythonLanguageService(
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
    public override SdkLanguage Language { get; } = SdkLanguage.Python;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Python package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for Python package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for Python package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.Python,
            SamplesDirectory = Path.Combine(fullPath, "samples")
        };
        
        logger.LogDebug("Resolved Python package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        string? packageName = null;
        string? packageVersion = null;

        var sdkPackagingPath = Path.Combine(packagePath, "sdk_packaging.toml");
        if (File.Exists(sdkPackagingPath))
        {
            try
            {
                logger.LogTrace("Reading sdk_packaging.toml from {sdkPackagingPath}", sdkPackagingPath);
                var content = await File.ReadAllTextAsync(sdkPackagingPath, ct);
                var match = PackageNameRegex().Match(content);
                if (match.Success)
                {
                    packageName = match.Groups[1].Value.Trim();
                    logger.LogTrace("Found package name from sdk_packaging.toml: {packageName}", packageName);
                }
                else
                {
                    logger.LogTrace("No package_name found in sdk_packaging.toml");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error reading sdk_packaging.toml from {sdkPackagingPath}", sdkPackagingPath);
            }
        }
        else
        {
            logger.LogWarning("No sdk_packaging.toml file found at {sdkPackagingPath}", sdkPackagingPath);
        }

        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var modulePath = packageName.Replace('-', Path.DirectorySeparatorChar);
            var versionPyPath = Path.Combine(packagePath, modulePath, "_version.py");
            if (File.Exists(versionPyPath))
            {
                try
                {
                    logger.LogTrace("Reading _version.py from {versionPyPath}", versionPyPath);
                    var content = await File.ReadAllTextAsync(versionPyPath, ct);
                    var match = VersionFieldRegex().Match(content);
                    if (match.Success)
                    {
                        packageVersion = match.Groups[1].Value.Trim();
                        logger.LogTrace("Found version from _version.py: {packageVersion}", packageVersion);
                    }
                    else
                    {
                        logger.LogTrace("No VERSION found in _version.py");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error reading _version.py from {versionPyPath}", versionPyPath);
                }
            }
            else
            {
                logger.LogTrace("No _version.py file found at {versionPyPath}", versionPyPath);
            }
        }

        return (packageName, packageVersion);
    }

    [GeneratedRegex(@"^\s*package_name\s*=\s*['""]([^'""]+)['""]", RegexOptions.Compiled | RegexOptions.Multiline, "")]
    private static partial Regex PackageNameRegex();
    
    [GeneratedRegex(@"^\s*VERSION\s*=\s*['""]([^'""]+)['""]", RegexOptions.Compiled | RegexOptions.Multiline, "")]
    private static partial Regex VersionFieldRegex();

    public override async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await processHelper.Run(new ProcessOptions(
                command: "pytest",
                args: ["tests"],
                workingDirectory: packagePath
            ),
            ct
        );

        return result.ExitCode == 0;
    }
    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return GetRequirements(packagePath, categories, null, ct);
    }

    public List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, string venvPath, CancellationToken ct = default)
    {
        var reqs = categories.TryGetValue("python", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();

        if (string.IsNullOrWhiteSpace(venvPath))
        {
            venvPath = FindVenv(packagePath);
        }

        if (string.IsNullOrWhiteSpace(venvPath) || !Directory.Exists(venvPath))
        {
            logger.LogWarning("Provided venv path is invalid or does not exist: {VenvPath}", venvPath);
            return reqs;
        }

        logger.LogInformation("Using virtual environment at: {VenvPath}", venvPath);
        return UpdateChecksWithVenv(reqs, venvPath);
    }

    private List<SetupRequirements.Requirement> UpdateChecksWithVenv(List<SetupRequirements.Requirement> reqs, string venvPath)
    {
        foreach (var req in reqs)
        {
            // Update checks to use venv path
            var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
            req.check[0] = Path.Combine(venvPath, binDir, req.check[0]);
        }

        return reqs;
    }

    private string FindVenv(string packagePath)
    {
        // try to find existing venv in package path if none was provided
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            packagePath = Environment.CurrentDirectory;
        }

        var candidates = new[] { ".venv", "venv", ".env", "env" };

        foreach (var c in candidates)
        {
            var path = Path.Combine(packagePath, c);
            if (Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }
}
