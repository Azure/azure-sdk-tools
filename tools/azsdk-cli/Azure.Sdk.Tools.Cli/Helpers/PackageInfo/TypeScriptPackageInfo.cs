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

    public TypeScriptPackageInfo() { }

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
}