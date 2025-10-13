// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class PythonPackageInfo : IPackageInfo
{
    private string? _packagePath;
    private string? _repoRoot;
    private string? _relativePath;
    public bool IsInitialized { get; private set; }

    public PythonPackageInfo() { }

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

    private string Ensure(string? value) => value ?? throw new InvalidOperationException("PythonPackageInfo not initialized. Call Init(packagePath) first.");

    public string RepoRoot => Ensure(_repoRoot);
    public string RelativePath => Ensure(_relativePath);
    public string PackagePath => Ensure(_packagePath);
    public string PackageName => Path.GetFileName(PackagePath);
    public string ServiceName => Path.GetFileName(Path.GetDirectoryName(PackagePath)) ?? string.Empty;
    public string Language => "python";

    public Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Path.Combine(PackagePath, "samples"));
    public string GetFileExtension() => ".py";

    public async Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default)
    {
        async Task<string?> TryFileAsync(string file, Func<string, string?> extractor)
        {
            var path = Path.Combine(_packagePath!, file);
            if (!File.Exists(path))
            {
                return null;
            }
            try { return extractor(await File.ReadAllTextAsync(path, cancellationToken)); } catch { return null; }
        }

        string? Extract(string content)
        {
            var match = _pythonTomlRegex.Value.Match(content);
            return match.Success ? match.Groups[1].Value : null;
        }

        var version = await TryFileAsync("pyproject.toml", Extract);
        if (version != null)
        {
            return version;
        }
        return await TryFileAsync("setup.py", Extract);
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

    private static readonly Lazy<Regex> _pythonTomlRegex = new(() => new Regex("(?i)version\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.Compiled));
}