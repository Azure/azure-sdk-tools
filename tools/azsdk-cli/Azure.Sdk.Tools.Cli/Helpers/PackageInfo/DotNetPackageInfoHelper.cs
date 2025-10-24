// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml.Linq;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed class DotNetPackageInfoHelper(IGitHelper gitHelper, ILogger<DotNetPackageInfoHelper> logger) : IPackageInfoHelper
{
    public async Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for .NET package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for .NET package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectory = Path.Combine(fullPath, "tests", "samples")
        };
        
        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }


    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(Path.Combine(packagePath, "src"), "*.csproj").FirstOrDefault();

            if (csproj == null) 
            {
                logger.LogWarning("No .csproj file found in {packagePath}", packagePath);
                return (null, null); 
            }
            
            logger.LogTrace("Reading .csproj file: {csproj}", csproj);
            var content = await File.ReadAllTextAsync(csproj, ct);

            // Parse XML
            var doc = XDocument.Parse(content);
            
            // Extract name from PackageId, AssemblyName, or file name
            string? name = null;
            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                name = packageId;
                logger.LogTrace("Found package name from PackageId: {name}", name);
            }
            else
            {
                var assemblyName = doc.Descendants("AssemblyName").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    name = assemblyName;
                    logger.LogTrace("Found package name from AssemblyName: {name}", name);
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(csproj);
                    logger.LogTrace("Using file name as package name: {name}", name);
                }
            }

            // Extract version from Version, or VersionPrefix + VersionSuffix
            string? version = null;
            var versionElement = doc.Descendants("Version").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(versionElement))
            {
                version = versionElement;
                logger.LogTrace("Found version from Version tag: {version}", version);
            }
            else
            {
                var versionPrefix = doc.Descendants("VersionPrefix").FirstOrDefault()?.Value;
                if (!string.IsNullOrWhiteSpace(versionPrefix))
                {
                    var versionSuffix = doc.Descendants("VersionSuffix").FirstOrDefault()?.Value;
                    version = !string.IsNullOrWhiteSpace(versionSuffix) 
                        ? $"{versionPrefix}-{versionSuffix}" 
                        : versionPrefix;
                    logger.LogTrace("Found version from VersionPrefix/Suffix: {version}", version);
                }
                else
                {
                    logger.LogTrace("No version information found in .csproj");
                }
            }

            return (name, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading .NET package info from {packagePath}", packagePath);
            return (null, null);
        }
    }
}
