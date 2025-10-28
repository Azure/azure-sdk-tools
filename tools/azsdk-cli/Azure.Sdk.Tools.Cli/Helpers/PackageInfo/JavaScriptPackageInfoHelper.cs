// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class JavaScriptPackageInfoHelper(IGitHelper gitHelper, ILogger<JavaScriptPackageInfoHelper> logger) : IPackageInfoHelper
{
    public async Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving JavaScript package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, packagePath);
        var (packageName, packageVersion) = await TryGetPackageInfoAsync(fullPath, ct);
        
        if (packageName == null)
        {
            logger.LogWarning("Could not determine package name for JavaScript package at {fullPath}", fullPath);
        }
        if (packageVersion == null)
        {
            logger.LogWarning("Could not determine package version for JavaScript package at {fullPath}", fullPath);
        }
        
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.JavaScript,
            SamplesDirectory = Path.Combine(fullPath, "samples-dev")
        };
        
        logger.LogDebug("Resolved JavaScript package: {packageName} v{packageVersion} at {relativePath}", 
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath);
        
        return model;
    }

    private async Task<(string? Name, string? Version)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(packagePath, "package.json");
            if (!File.Exists(path)) 
            {
                logger.LogWarning("No package.json file found at {path}", path);
                return (null, null); 
            }
            
            logger.LogTrace("Reading package.json from {path}", path);
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }, ct);

            string? name = null;
            string? version = null;

            if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var nameValue = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(nameValue)) 
                { 
                    name = nameValue;
                    logger.LogTrace("Found package name: {name}", name);
                }
            }
            else
            {
                logger.LogTrace("No name property found in package.json");
            }

            if (doc.RootElement.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
            {
                var versionValue = versionProp.GetString();
                if (!string.IsNullOrWhiteSpace(versionValue)) 
                { 
                    version = versionValue;
                    logger.LogTrace("Found version: {version}", version);
                }
            }
            else
            {
                logger.LogTrace("No version property found in package.json");
            }

            return (name, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading JavaScript package info from {packagePath}", packagePath);
            return (null, null);
        }
    }

}
