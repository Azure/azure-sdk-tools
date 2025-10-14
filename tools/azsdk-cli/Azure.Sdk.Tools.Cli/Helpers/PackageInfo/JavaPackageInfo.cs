// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class JavaPackageInfo : IPackageInfo
{
    private string? _packagePath;
    private string? _repoRoot;
    private string? _relativePath;
    public bool IsInitialized { get; private set; }
    private readonly IGitHelper _gitHelper;

    public JavaPackageInfo(IGitHelper gitHelper)
    {
        _gitHelper = gitHelper;
    }

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

    private string Ensure(string? value) => value ?? throw new InvalidOperationException("JavaPackageInfo not initialized. Call Init(packagePath) first.");

    public string RepoRoot => Ensure(_repoRoot);
    public string RelativePath => Ensure(_relativePath);
    public string PackagePath => Ensure(_packagePath);
    public string PackageName => Path.GetFileName(PackagePath);
    public string ServiceName => Path.GetFileName(Path.GetDirectoryName(PackagePath)) ?? string.Empty;
    public string Language => "java";

    public Task<string> GetSamplesDirectoryAsync(CancellationToken cancellationToken = default)
    {
        // Attempt to infer module path for samples: src/samples/java/<module path>
        var moduleName = TryGetJavaModuleName(PackagePath);
        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            var modulePath = moduleName!.Replace('.', Path.DirectorySeparatorChar);
            return Task.FromResult(Path.Combine(PackagePath, "src", "samples", "java", modulePath));
        }
        return Task.FromResult(Path.Combine(PackagePath, "src", "samples", "java"));
    }

    public string GetFileExtension() => ".java";

    public async Task<string?> GetPackageVersionAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(PackagePath, "pom.xml");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var projectStart = content.IndexOf("<project", StringComparison.OrdinalIgnoreCase);
            if (projectStart >= 0)
            {
                var dependenciesIndex = content.IndexOf("<dependencies", projectStart, StringComparison.OrdinalIgnoreCase);
                var searchSegment = dependenciesIndex > projectStart
                    ? content.Substring(projectStart, dependenciesIndex - projectStart)
                    : content.Substring(projectStart);
                var match = _pomVersionRegex.Value.Match(searchSegment);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static string? TryGetJavaModuleName(string packagePath)
    {
        try
        {
            var moduleInfoPath = Path.Combine(packagePath, "src", "main", "java", "module-info.java");
            if (!File.Exists(moduleInfoPath))
            {
                return null;
            }
            var content = File.ReadAllText(moduleInfoPath);
            var match = Regex.Match(content, @"^\s*module\s+([^\{\s]+)\s*\{", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch { return null; }
    }

    private (string RepoRoot, string RelativePath, string FullPath) Parse(string packagePath)
    {
        var full = Path.GetFullPath(packagePath);
        var repoRoot = _gitHelper.DiscoverRepoRoot(full);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        if (!full.StartsWith(sdkRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, sdkRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path '{packagePath}' is not under the expected 'sdk' folder of repo root '{repoRoot}'. Expected structure: <repoRoot>/sdk/<service>/<package>", nameof(packagePath));
        }
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }

    private static readonly Lazy<Regex> _pomVersionRegex = new(() => new Regex("<version>([^<]+)</version>", RegexOptions.Compiled));
}