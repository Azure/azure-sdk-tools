// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class TypeScriptPackageInfo : IPackageInfo
{
    private string? _packagePath;
    private string? _repoRoot;
    private string? _relativePath;
    public bool IsInitialized { get; private set; }
    private readonly IGitHelper _gitHelper;

    public TypeScriptPackageInfo(IGitHelper gitHelper)
    {
        _gitHelper = gitHelper;
    }

    public void Init(string packagePath)
    {
        var realPath = RealPath.GetRealPath(packagePath);
        if (IsInitialized)
        {
            if (!string.Equals(_packagePath, realPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("PackageInfo already initialized with a different path.");
            }
            return;
        }
        (_repoRoot, _relativePath, _packagePath) = Parse(realPath);
        IsInitialized = true;
    }

    private string Ensure(string? value) => value ?? throw new InvalidOperationException("TypeScriptPackageInfo not initialized. Call Init(packagePath) first.");

    public string RepoRoot => Ensure(_repoRoot);
    public string RelativePath => Ensure(_relativePath);
    public string PackagePath => Ensure(_packagePath);
    public string PackageName => Path.GetFileName(PackagePath);
    public string ServiceName => Path.GetFileName(Path.GetDirectoryName(PackagePath)) ?? string.Empty;
    public string Language => "typescript";

    public Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Path.Combine(PackagePath, "samples-dev"));
    public string GetFileExtension() => ".ts";

    public async Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = Path.Combine(_packagePath!, "package.json");
            if (!File.Exists(path))
            {
                return null;
            }
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var match = Regex.Match(content, "\"version\"\\s*:\\s*\"([^\\\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch { return null; }
    }

    private (string RepoRoot, string RelativePath, string FullPath) Parse(string realPackagePath)
    {
        var full = realPackagePath;
        var repoRoot = _gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        if (!full.StartsWith(sdkRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, sdkRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path '{realPackagePath}' is not under the expected 'sdk' folder of repo root '{repoRoot}'. Expected structure: <repoRoot>/sdk/<service>/<package>", nameof(realPackagePath));
        }
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }
}