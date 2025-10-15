// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class GoPackageInfo : IPackageInfo
{
    private string? _packagePath;
    private string? _repoRoot;
    private string? _relativePath;
    public bool IsInitialized { get; private set; }
    private readonly IGitHelper _gitHelper;

    public GoPackageInfo(IGitHelper gitHelper)
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

    private string Ensure(string? value) => value ?? throw new InvalidOperationException("GoPackageInfo not initialized. Call Init(packagePath) first.");

    public string RepoRoot => Ensure(_repoRoot);
    public string RelativePath => Ensure(_relativePath);
    public string PackagePath => Ensure(_packagePath);
    public string PackageName => Path.GetFileName(PackagePath);
    public string ServiceName => Path.GetFileName(Path.GetDirectoryName(PackagePath)) ?? string.Empty;
    public string Language => "go";

    public Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PackagePath);
    public string GetFileExtension() => ".go";

    public async Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_packagePath!, "version.go");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var match = _goVersionRegex.Value.Match(content);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            match = Regex.Match(content, "(?s)const\\s*\\([^)]*?[Vv]ersion\\s*=\\s*\"([^\" ]+)\"");
            return match.Success ? match.Groups[1].Value.Trim() : null;
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