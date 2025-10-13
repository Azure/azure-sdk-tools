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

    public GoPackageInfo() { }

    public void Init(string packagePath)
    {
        if (IsInitialized)
        {
            if (!string.Equals(_packagePath, Path.GetFullPath(packagePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("PackageInfo already initialized with a different path.");
            }
            return;
        }
        (_repoRoot, _relativePath, _packagePath) = Parse(packagePath);
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

    private static (string RepoRoot, string RelativePath, string FullPath) Parse(string packagePath)
    {
        var full = Path.GetFullPath(packagePath);
        var sdkMarker = $"{Path.DirectorySeparatorChar}sdk{Path.DirectorySeparatorChar}";

        var sdkIndex = full.IndexOf(sdkMarker, StringComparison.OrdinalIgnoreCase);
        if (sdkIndex < 0)
        {
            throw new ArgumentException($"Path '{packagePath}' is not under an Azure SDK repository with 'sdk' subfolder.", nameof(packagePath));
        }

        var repoRoot = full[..sdkIndex];
        var relativePath = full[(sdkIndex + sdkMarker.Length)..].TrimStart(Path.DirectorySeparatorChar);

        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            throw new ArgumentException($"Path '{packagePath}' must be at least three folders deep under 'sdk' (expected: sdk/<group>/<service>/<package>). Actual relative path: 'sdk/{relativePath}'", nameof(packagePath));
        }

        return (repoRoot, relativePath, full);
    }

    private static readonly Lazy<Regex> _goVersionRegex = new(() => new Regex("(?m)^\\s*[Vv]ersion\\s*=\\s*\"([^\" ]+)\"", RegexOptions.Compiled));
}