// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed class DotNetPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
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
            Language = "dotnet",
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(Path.Combine(pi.PackagePath, "tests", "samples")),
            FileExtensionProvider = _ => ".cs",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
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

    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(packagePath, "*.csproj").FirstOrDefault();
            if (csproj == null) { return null; }
            var content = await File.ReadAllTextAsync(csproj, ct);
            var versionMatch = _csProjVersionRegex.Value.Match(content);
            if (versionMatch.Success) { return versionMatch.Groups[1].Value; }
            var prefix = _csProjVersionPrefixRegex.Value.Match(content);
            if (prefix.Success)
            {
                var suffix = _csProjVersionSuffixRegex.Value.Match(content);
                return suffix.Success ? $"{prefix.Groups[1].Value}-{suffix.Groups[1].Value}" : prefix.Groups[1].Value;
            }
            return null;
        }
        catch { return null; }
    }

    private static readonly Lazy<Regex> _csProjVersionRegex = new(() => new Regex("<Version>([^<]+)</Version>", RegexOptions.Compiled));
    private static readonly Lazy<Regex> _csProjVersionPrefixRegex = new(() => new Regex("<VersionPrefix>([^<]+)</VersionPrefix>", RegexOptions.Compiled));
    private static readonly Lazy<Regex> _csProjVersionSuffixRegex = new(() => new Regex("<VersionSuffix>([^<]+)</VersionSuffix>", RegexOptions.Compiled));
}
