// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Language-specific helper for Go packages. Provides structural package info plus lazy accessors
/// for samples directory, file extension, and version parsing.
/// </summary>
public sealed class GoPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
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
            Language = "go",
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(pi.PackagePath),
            FileExtensionProvider = _ => ".go",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
    }

    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        // Common pattern: version.go containing a Version constant or variable.
        var versionFile = Path.Combine(packagePath, "version.go");
        if (!File.Exists(versionFile))
        {
            return null;
        }
        try
        {
            var content = await File.ReadAllTextAsync(versionFile, ct);
            // Match: Version = "v1.2.3" OR version = "v1.2.3"
            var match = _goVersionRegex.Value.Match(content);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            // Fallback: look for const block definition e.g. const ( Version = "v1.2.3" )
            match = Regex.Match(content, "(?s)const\\s*\\([^)]*?[Vv]ersion\\s*=\\s*\"([^\" ]+)\"");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static (string RepoRoot, string RelativePath, string FullPath) Parse(IGitHelper gitHelper, string realPackagePath)
    {
        var full = realPackagePath;
        var repoRoot = gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        if (!full.StartsWith(sdkRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, sdkRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path '{realPackagePath}' is not under the expected 'sdk' folder of repo root '{repoRoot}'. Expected structure: <repoRoot>/sdk/<group>/<service>/<package>", nameof(realPackagePath));
        }
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            throw new ArgumentException($"Path '{realPackagePath}' must be at least three folders deep under 'sdk' (expected: sdk/<group>/<service>/<package>). Actual relative path: 'sdk/{relativePath}'", nameof(realPackagePath));
        }
        return (repoRoot, relativePath, full);
    }

    private static readonly Lazy<Regex> _goVersionRegex = new(() => new Regex("(?m)^\\s*[Vv]ersion\\s*=\\s*\"([^\" ]+)\"", RegexOptions.Compiled));
}
