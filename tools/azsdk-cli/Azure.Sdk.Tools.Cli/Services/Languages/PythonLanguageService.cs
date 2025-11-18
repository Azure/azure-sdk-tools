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
    private readonly IPythonHelper pythonHelper;

    public PythonLanguageService(
        IProcessHelper processHelper,
        IPythonHelper pythonHelper,
        INpxHelper npxHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers)
    {
        this.pythonHelper = pythonHelper;
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

        var sdkType = SdkType.Unknown;
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            sdkType = packageName.StartsWith("azure-mgmt-", StringComparison.OrdinalIgnoreCase)
                ? SdkType.Management
                : SdkType.Dataplane;
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
            SamplesDirectory = Path.Combine(fullPath, "samples"),
            SdkType = sdkType
        };
        
        logger.LogDebug("Resolved Python package: {sdkType} {packageName} v{packageVersion} at {relativePath}", 
            sdkType, packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
{
    string? packageName = null;
    string? packageVersion = null;

    var pyprojectPath = Path.Combine(packagePath, "pyproject.toml");
    if (File.Exists(pyprojectPath))
    {
        try
        {
            logger.LogTrace("Reading pyproject.toml from {pyprojectPath}", pyprojectPath);
            var content = await File.ReadAllTextAsync(pyprojectPath, ct);
            
            var nameMatch = PyprojectNameRegex().Match(content);
            if (nameMatch.Success)
            {
                packageName = nameMatch.Groups[1].Value.Trim();
                logger.LogTrace("Found package name from pyproject.toml: {packageName}", packageName);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error reading pyproject.toml from {pyprojectPath}", pyprojectPath);
        }
    }
    
    if (string.IsNullOrWhiteSpace(packageName))
    {
        var setupPyPath = Path.Combine(packagePath, "setup.py");
        if (File.Exists(setupPyPath))
        {
            try
            {
                logger.LogTrace("Reading setup.py from {setupPyPath}", setupPyPath);
                var content = await File.ReadAllTextAsync(setupPyPath, ct);
                
                var match = SetupPyPackageNameRegex().Match(content);
                if (match.Success)
                {
                    packageName = match.Groups[1].Value.Trim();
                    logger.LogTrace("Found package name from setup.py: {packageName}", packageName);
                }
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Error reading setup.py from {setupPyPath}", setupPyPath);
            }
        }
        else
        {
            logger.LogTrace("No pyproject.toml or setup.py file found at {packagePath}", packagePath);
        }
    }
    if (string.IsNullOrWhiteSpace(packageVersion))
    {
        // Search for _version.py files in the package directory
        try
        {
            var versionFiles = Directory.GetFiles(packagePath, "_version.py", SearchOption.AllDirectories);
            
            foreach (var versionPyPath in versionFiles)
            {
                try
                {
                    logger.LogTrace("Reading version file from {versionPyPath}", versionPyPath);
                    var content = await File.ReadAllTextAsync(versionPyPath, ct);
                    var match = VersionFieldRegex().Match(content);
                    if (match.Success)
                    {
                        packageVersion = match.Groups[1].Value.Trim();
                        logger.LogTrace("Found version from version file: {packageVersion}", packageVersion);
                        break; // Stop after finding the first valid version
                    }
                    else
                    {
                        logger.LogTrace("No VERSION found in version file at {versionPyPath}", versionPyPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogTrace(ex, "Error reading version file from {versionPyPath}", versionPyPath);
                }
            }
            
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                logger.LogTrace("No version file found with valid VERSION in {packagePath}", packagePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error searching for version files in {packagePath}", packagePath);
        }
    }

    return (packageName, packageVersion);
}

    [GeneratedRegex(@"^\s*name\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.Multiline, "")]
    private static partial Regex PyprojectNameRegex();

    [GeneratedRegex(@"(?:PACKAGE_NAME|name)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.Multiline, "")]
    private static partial Regex SetupPyPackageNameRegex();

    [GeneratedRegex(@"^\s*VERSION\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.Multiline, "")]
    private static partial Regex VersionFieldRegex();

    public override async Task<bool> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await pythonHelper.Run(new PythonOptions(
                "pytest",
                ["tests"],
                workingDirectory: packagePath
            ),
            ct
        );

        return result.ExitCode == 0;
    }
    public override List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        var reqs = categories.TryGetValue("python", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();

        foreach (var req in reqs)
        {
            if (req.check != null && req.check.Length > 0)
            {
                var executableName = req.check[0];
                req.check[0] = PythonOptions.ResolvePythonExecutable(executableName);
            }
        }

        return reqs;
    }
}
