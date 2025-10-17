// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class JavaPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
{
    public Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default)
    {
        var realPath = RealPath.GetRealPath(packagePath);
        var (repoRoot, relativePath, fullPath) = Parse(gitHelper, realPath);
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = Path.GetFileName(fullPath),
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = "java",
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(BuildSamplesDirectory(pi.PackagePath)),
            FileExtensionProvider = _ => ".java",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
    }

    private static string BuildSamplesDirectory(string packagePath)
    {
        var moduleName = TryGetJavaModuleName(packagePath);
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            var modulePath = moduleName!.Replace('.', Path.DirectorySeparatorChar);
            return Path.Combine(packagePath, "src", "samples", "java", modulePath);
        }
        return Path.Combine(packagePath, "src", "samples", "java");
    }

    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        var path = Path.Combine(packagePath, "pom.xml");
        if (!File.Exists(path)) { return null; }
        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var projectStart = content.IndexOf("<project", StringComparison.OrdinalIgnoreCase);
            if (projectStart >= 0)
            {
                var dependenciesIndex = content.IndexOf("<dependencies", projectStart, StringComparison.OrdinalIgnoreCase);
                var searchSegment = dependenciesIndex > projectStart
                    ? content.Substring(projectStart, dependenciesIndex - projectStart)
                    : content.Substring(projectStart);
                var match = _pomVersionRegex.Value.Match(searchSegment);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static string? TryGetJavaModuleName(string packagePath)
    {
        try
        {
            var moduleInfoPath = Path.Combine(packagePath, "src", "main", "java", "module-info.java");
            if (!File.Exists(moduleInfoPath)) { return null; }
            var content = File.ReadAllText(moduleInfoPath);
            var match = Regex.Match(content, @"^\s*module\s+([^\{\s]+)\s*\{", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch { return null; }
    }

    private static (string RepoRoot, string RelativePath, string FullPath) Parse(IGitHelper gitHelper, string realPackagePath)
    {
        var full = realPackagePath;
        var repoRoot = gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        if (!full.StartsWith(sdkRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, sdkRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path '{realPackagePath}' is not under the expected 'sdk' folder of repo root '{repoRoot}'. Expected structure: <repoRoot>/sdk/<service>/<package>", nameof(realPackagePath));
        }
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }

    private static readonly Lazy<Regex> _pomVersionRegex = new(() => new Regex("<version>([^<]+)</version>", RegexOptions.Compiled));
}
