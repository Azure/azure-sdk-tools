// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class TypeScriptPackageInfoHelper(IGitHelper gitHelper) : IPackageInfoHelper
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
            Language = Models.SdkLanguage.JavaScript,
            SamplesDirectoryProvider = (pi, _) => Task.FromResult(Path.Combine(pi.PackagePath, "samples-dev")),
            FileExtensionProvider = _ => ".ts",
            VersionProvider = (pi, token) => TryGetVersionAsync(pi.PackagePath, token)
        };
        return Task.FromResult(model);
    }

    private static async Task<string?> TryGetVersionAsync(string packagePath, CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(packagePath, "package.json");
            if (!File.Exists(path)) { return null; }
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            }, ct);
            if (doc.RootElement.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
            {
                var value = versionProp.GetString();
                if (!string.IsNullOrWhiteSpace(value)) { return value; }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

}
