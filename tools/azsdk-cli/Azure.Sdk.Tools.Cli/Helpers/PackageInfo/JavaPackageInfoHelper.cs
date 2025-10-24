// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed partial class JavaPackageInfoHelper(IGitHelper gitHelper, ILogger<JavaPackageInfoHelper> logger) : IPackageInfoHelper
{
    public async Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving Java package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for Java package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for Java package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.Java,
            SamplesDirectory = BuildSamplesDirectory(fullPath)
        };
        
        logger.LogDebug("Resolved Java package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

    private string BuildSamplesDirectory(string packagePath)
    {
        var moduleName = TryGetJavaModuleName(packagePath);
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            var modulePath = moduleName!.Replace('.', Path.DirectorySeparatorChar);
            var samplesDir = Path.Combine(packagePath, "src", "samples", "java", modulePath);
            logger.LogTrace("Built samples directory with module name: {samplesDir}", samplesDir);
            return samplesDir;
        }
        var defaultDir = Path.Combine(packagePath, "src", "samples", "java");
        logger.LogTrace("Built default samples directory: {defaultDir}", defaultDir);
        return defaultDir;
    }

    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        var path = Path.Combine(packagePath, "pom.xml");
        if (!File.Exists(path)) 
        {
            logger.LogWarning("No pom.xml file found at {path}", path);
            return (null, null); 
        }
        try
        {
            logger.LogTrace("Reading pom.xml file: {path}", path);
            using var stream = File.OpenRead(path);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            var root = doc.Root;
            if (root == null) 
            {
                logger.LogWarning("pom.xml has no root element at {path}", path);
                return (null, null); 
            }
            // Maven POM uses a default namespace; capture it to access elements.
            var ns = root.Name.Namespace;

            // Extract artifactId
            string? artifactId = root.Element(ns + "artifactId")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(artifactId)) 
            { 
                logger.LogWarning("No artifactId found in pom.xml at {path}", path);
                artifactId = null; 
            }
            else
            {
                logger.LogTrace("Found artifactId: {artifactId}", artifactId);
            }

            // Extract version
            string? version = root.Element(ns + "version")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                // Fallback to parent version if project version not declared directly.
                var parent = root.Element(ns + "parent");
                version = parent?.Element(ns + "version")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    logger.LogTrace("Found version from parent: {version}", version);
                }
            }
            else
            {
                logger.LogTrace("Found version: {version}", version);
            }
            
            if (string.IsNullOrWhiteSpace(version)) 
            { 
                logger.LogWarning("No version found in pom.xml at {path}", path);
                version = null; 
            }

            return (artifactId, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading Java package info from {path}", path);
            return (null, null);
        }
    }

    private string? TryGetJavaModuleName(string packagePath)
    {
        try
        {
            var moduleInfoPath = Path.Combine(packagePath, "src", "main", "java", "module-info.java");
            if (!File.Exists(moduleInfoPath)) 
            {
                logger.LogTrace("No module-info.java found at {moduleInfoPath}", moduleInfoPath);
                return null; 
            }
            
            logger.LogTrace("Reading module-info.java from {moduleInfoPath}", moduleInfoPath);
            var content = File.ReadAllText(moduleInfoPath);
            var match = JavaModuleDeclarationRegex().Match(content);
            if (match.Success)
            {
                var moduleName = match.Groups[1].Value.Trim();
                logger.LogTrace("Found Java module name: {moduleName}", moduleName);
                return moduleName;
            }
            logger.LogTrace("Could not parse module name from module-info.java");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error reading Java module name from {packagePath}", packagePath);
            return null;
        }
    }

    /// <summary>
    /// Matches Java module declarations like "module com.azure.storage.blob {"
    /// </summary>
    [GeneratedRegex(@"^\s*module\s+([^\{\s]+)\s*\{", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex JavaModuleDeclarationRegex();
}
