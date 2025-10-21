// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed partial class DotNetPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
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
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(Path.Combine(pi.PackagePath, "tests", "samples")),
            FileExtensionProvider = _ => ".cs",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
    }


    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var csproj = Directory.GetFiles(packagePath, "*.csproj").FirstOrDefault();
            if (csproj == null) { return null; }
            var content = await File.ReadAllTextAsync(csproj, ct);
            var versionMatch = MyRegex().Match(content);
            if (versionMatch.Success) { return versionMatch.Groups[1].Value; }
            var prefix = MyRegex1().Match(content);
            if (prefix.Success)
            {
                var suffix = MyRegex2().Match(content);
                return suffix.Success ? $"{prefix.Groups[1].Value}-{suffix.Groups[1].Value}" : prefix.Groups[1].Value;
            }
            return null;
        }
        catch { return null; }
    }

    [GeneratedRegex("<Version>([^<]+)</Version>", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
    [GeneratedRegex("<VersionPrefix>([^<]+)</VersionPrefix>", RegexOptions.Compiled)]
    private static partial Regex MyRegex1();
    [GeneratedRegex("<VersionSuffix>([^<]+)</VersionSuffix>", RegexOptions.Compiled)]
    private static partial Regex MyRegex2();
}
