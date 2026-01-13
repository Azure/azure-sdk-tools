// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
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
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper)
    {
        this.pythonHelper = pythonHelper;
        this.npxHelper = npxHelper;
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
    
    try
    {
        logger.LogTrace("Calling get_package_properties.py for {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var scriptPath = Path.Combine(repoRoot, "eng", "scripts", "get_package_properties.py");
        
        var result = await pythonHelper.Run(new PythonOptions(
                "python",
                [scriptPath, "-s", packagePath],
                workingDirectory: repoRoot,
                logOutputStream: false
            ),
            ct
        );
        
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            // Parse the output from get_package_properties.py
            // Format: <name> <version> <is_new_sdk> <directory> <dependent_packages>
            var lines = result.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                var parts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                packageName = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : null;
                packageVersion = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
                
                logger.LogTrace("Python script returned: name={packageName}, version={packageVersion}", packageName, packageVersion);
                return (packageName, packageVersion);
            }
        }
        
        logger.LogWarning("Python script failed with exit code {exitCode}. Output: {output}", result.ExitCode, result.Output);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error running Python script for {packagePath}", packagePath);
        
    }
    return (packageName, packageVersion);
}

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var result = await pythonHelper.Run(new PythonOptions(
                "pytest",
                ["tests"],
                workingDirectory: packagePath
            ),
            ct
        );

        return new TestRunResponse(result);
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
