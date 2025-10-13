// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class DotNetPackageInfo : IPackageInfo
{
    private string? _packagePath;
    private string? _repoRoot;
    private string? _relativePath;
    public bool IsInitialized { get; private set; }

    public DotNetPackageInfo() { }

    public void Init(string packagePath)
    {
        if (IsInitialized)
        {
            if (!string.Equals(_packagePath, Path.GetFullPath(packagePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("PackageInfo already initialized with a different path.");
            }
            return; // idempotent for same path
        }
        (_repoRoot, _relativePath, _packagePath) = Parse(packagePath);
        IsInitialized = true;
    }

    private string Ensure(string? value) => value ?? throw new InvalidOperationException("DotNetPackageInfo not initialized. Call Init(packagePath) first.");

    public string RepoRoot => Ensure(_repoRoot);
    public string RelativePath => Ensure(_relativePath);
    public string PackagePath => Ensure(_packagePath);
    public string PackageName => Path.GetFileName(PackagePath);
    public string ServiceName => Path.GetFileName(Path.GetDirectoryName(PackagePath)) ?? string.Empty;
    public string Language => "dotnet";

    public Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Path.Combine(PackagePath, "tests", "samples"));
    public string GetFileExtension() => ".cs";

    public async Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var csproj = Directory.GetFiles(PackagePath, "*.csproj").FirstOrDefault();
            if (csproj == null)
            {
                return null;
            }
            var content = await File.ReadAllTextAsync(csproj, cancellationToken);
            var versionMatch = _csProjVersionRegex.Value.Match(content);
            if (versionMatch.Success)
            {
                return versionMatch.Groups[1].Value;
            }
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

    private static (string RepoRoot, string RelativePath, string FullPath) Parse(string packagePath)
    {
        var full = Path.GetFullPath(packagePath);
        var sdkSeparator = $"{Path.DirectorySeparatorChar}sdk{Path.DirectorySeparatorChar}";
        var pieces = full.Split(sdkSeparator, StringSplitOptions.None);
        if (pieces.Length != 2)
        {
            throw new ArgumentException($"Path '{packagePath}' is not under an Azure SDK repository with 'sdk' subfolder. Expected structure: /path/to/azure sdk repo/sdk/<service>/<package>", nameof(packagePath));
        }
        return (pieces[0], pieces[1], full);
    }

    private static readonly Lazy<Regex> _csProjVersionRegex = new(() => new Regex("<Version>([^<]+)</Version>", RegexOptions.Compiled));
    private static readonly Lazy<Regex> _csProjVersionPrefixRegex = new(() => new Regex("<VersionPrefix>([^<]+)</VersionPrefix>", RegexOptions.Compiled));
    private static readonly Lazy<Regex> _csProjVersionSuffixRegex = new(() => new Regex("<VersionSuffix>([^<]+)</VersionSuffix>", RegexOptions.Compiled));
}