// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed partial class PythonPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
{
    public Task<PackageInfo> ResolvePackageInfo(string packagePath, CancellationToken ct = default)
    {
        var realPath = RealPath.GetRealPath(packagePath);
        var (repoRoot, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, realPath);
        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = Path.GetFileName(fullPath),
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.Python,
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
            var match = MyRegex().Match(content);
            return match.Success ? match.Groups[1].Value : null;
        }

        return await TryFileAsync("pyproject.toml", Extract) ?? await TryFileAsync("setup.py", Extract);
    }

    [GeneratedRegex("(?i)version\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.Compiled, "")]
    private static partial Regex MyRegex();
}
