// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class PythonPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
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
            Language = "python",
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(Path.Combine(pi.PackagePath, "samples")),
            FileExtensionProvider = _ => ".py",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
    }

    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        async Task<string?> TryFileAsync(string file, Func<string, string?> extractor)
        {
            var path = Path.Combine(packagePath, file);
            if (!File.Exists(path)) { return null; }
            try { return extractor(await File.ReadAllTextAsync(path, ct)); } catch { return null; }
        }

        string? Extract(string content)
        {
            var match = _pythonTomlRegex.Value.Match(content);
            return match.Success ? match.Groups[1].Value : null;
        }

        return await TryFileAsync("pyproject.toml", Extract) ?? await TryFileAsync("setup.py", Extract);
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

    private static readonly Lazy<Regex> _pythonTomlRegex = new(() => new Regex("(?i)version\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.Compiled));
}
