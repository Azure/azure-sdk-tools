// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

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
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.pythonHelper = pythonHelper;
        this.npxHelper = npxHelper;
    }
    public override SdkLanguage Language { get; } = SdkLanguage.Python;
    public override bool IsCustomizedCodeUpdateSupported => true;

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Python package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
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
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
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

    public override string? HasCustomizations(string packagePath, CancellationToken ct = default)
    {
        // Python SDKs can have _patch.py files in multiple locations within the package:
        // e.g., azure/packagename/_patch.py, azure/packagename/models/_patch.py, azure/packagename/operations/_patch.py
        //
        // However, autorest.python generates empty _patch.py templates by default with:
        //   __all__: List[str] = []
        //
        // A _patch.py file only has actual customizations if __all__ is non-empty.
        // See: https://github.com/Azure/autorest.python/blob/main/docs/customizations.md

        try
        {
            var patchFiles = Directory.GetFiles(packagePath, "_patch.py", SearchOption.AllDirectories);
            foreach (var patchFile in patchFiles)
            {
                if (HasNonEmptyAllExport(patchFile))
                {
                    logger.LogDebug("Found Python _patch.py with customizations at {PatchFile}", patchFile);
                    return packagePath; // Return package path since patches are scattered
                }
            }

            logger.LogDebug("No Python _patch.py files with customizations found in {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for Python customization files in {PackagePath}", packagePath);
            return null;
        }
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

    /// <summary>
    /// Checks if a _patch.py file has a non-empty __all__ export list, indicating actual customizations.
    /// </summary>
    private bool HasNonEmptyAllExport(string patchFilePath)
    {
        try
        {
            foreach (var line in File.ReadLines(patchFilePath))
            {
                if (line.Contains("__all__") && line.Contains("="))
                {
                    // If line contains quoted strings, there are exports
                    if (line.Contains('"') || line.Contains('\''))
                    {
                        return true;
                    }
                    
                    // If line has [ but not ] on same line, it's multiline = non-empty
                    if (line.Contains('[') && !line.Contains(']'))
                    {
                        return true;
                    }
                    
                    // Single-line empty: __all__ = [] or __all__: List[str] = []
                    return false;
                }
            }
            
            // No __all__ found - assume no customizations (template file)
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading Python patch file {PatchFile}", patchFilePath);
            return false;
        }
    }

}
