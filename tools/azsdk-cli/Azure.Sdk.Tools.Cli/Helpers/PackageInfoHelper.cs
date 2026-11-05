// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IPackageInfoHelper
{
    List<PackageInfo> FilterPackagesByArtifact(List<PackageInfo> packages, string[] artifactList);
    void PopulateCommonCiMetadata(PackageInfo info);
    void WritePackageInfoFile(PackageInfo packageInfo, string outputPath, bool addDevVersion);
    TParameters? GetLanguageCiParameters<TParameters>(PackageInfo info)
        where TParameters : CiPipelineYamlParametersBase;
    Task<(string RepoRoot, string RelativePath, string FullPath)> ParsePackagePathAsync(string realPackagePath, CancellationToken ct);
}

public class PackageInfoHelper(ILogger<PackageInfoHelper> logger, IGitHelper gitHelper) : IPackageInfoHelper
{
    private const string ConflictingApiVersions = "Conflicting API versions; no latest-version selection.";

    public static Dictionary<string, string> ReadApiVersionsByProvider(JsonObject metadata)
    {
        var normalized = metadata.DeepClone().AsObject();
        if (normalized["apiVersions"] is JsonObject versionMap)
        {
            foreach (var entry in versionMap.ToArray())
            {
                if (entry.Value is not JsonArray array)
                {
                    continue;
                }
                var candidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in array)
                {
                    if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidOperationException(ConflictingApiVersions);
                    }
                    candidates.Add(text);
                }
                if (candidates.Count != 1)
                {
                    throw new InvalidOperationException(ConflictingApiVersions);
                }
                versionMap[entry.Key] = candidates.Single();
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var version in ReadApiVersions(JsonSerializer.SerializeToElement(normalized)))
        {
            var scalarVersion = metadata["apiVersion"];
            if (scalarVersion is not null)
            {
                if (scalarVersion is not JsonValue scalar || !scalar.TryGetValue<string>(out var text))
                {
                    throw new InvalidOperationException(ConflictingApiVersions);
                }
                if (text.Trim() != version.Version)
                {
                    throw new InvalidOperationException(ConflictingApiVersions);
                }
            }
            result.Add(version.Namespace ?? "", version.Version);
        }
        return result;
    }

    public static HashSet<PackageApiVersion> ReadApiVersions(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Metadata must be a JSON object.");
        }

        var versions = new HashSet<PackageApiVersion>();
        if (root.TryGetProperty("apiVersions", out var apiVersions))
        {
            if (apiVersions.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("'apiVersions' must be a namespace-to-version object.");
            }
            foreach (var property in apiVersions.EnumerateObject())
            {
                AddVersion(property.Value, $"apiVersions.{property.Name}", property.Name);
            }
        }
        if (versions.Count == 0 && root.TryGetProperty("apiVersion", out var apiVersion))
        {
            AddVersion(apiVersion, "apiVersion");
        }
        return versions;

        void AddVersion(JsonElement value, string field, string? namespaceName = null)
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                throw new JsonException($"'{field}' must be a non-empty API version string.");
            }
            versions.Add(new PackageApiVersion(value.GetString()!.Trim(), namespaceName));
        }
    }

    public List<PackageInfo> FilterPackagesByArtifact(List<PackageInfo> packages, string[] artifactList)
    {
        if (artifactList is null)
        {
            return packages;
        }

        var artifactArray = artifactList ?? artifactList.ToArray();
        if (artifactArray.Length == 0)
        {
            return packages;
        }

        var filteredArtifacts = artifactArray
            .Where(artifact => !string.IsNullOrWhiteSpace(artifact))
            .Select(artifact => artifact.Trim())
            .ToArray();

        if (filteredArtifacts.Length == 0)
        {
            logger.LogWarning("Artifact list contains no valid entries");
            return packages;
        }

        var artifactSet = new HashSet<string>(filteredArtifacts, StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages)
        {
            if (string.IsNullOrEmpty(pkg.ArtifactName))
            {
                logger.LogWarning(
                    "Package '{PackageName}' does not have an 'ArtifactName' property and will be excluded from artifact filtering.",
                    pkg.PackageName ?? "(unknown)");
            }
        }

        var filtered = packages
            .Where(pkg => !string.IsNullOrEmpty(pkg.ArtifactName) && artifactSet.Contains(pkg.ArtifactName))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException("No packages found matching the provided artifact list");
        }

        return filtered;
    }

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Populates CI metadata shared across languages.
    /// </summary>
    public void PopulateCommonCiMetadata(PackageInfo info)
    {
        var ciYamlResult = TryFindCiYaml<CiPipelineYamlParametersBase>(info);
        PopulateCommonCiMetadata(info, ciYamlResult);
    }

    public TParameters? GetLanguageCiParameters<TParameters>(PackageInfo info)
        where TParameters : CiPipelineYamlParametersBase
    {
        var ciYamlResult = TryFindCiYaml<TParameters>(info);
        return ciYamlResult?.Yaml.Parameters as TParameters;
    }

    private static void PopulateCommonCiMetadata(PackageInfo info, (ICiPipelineYaml Yaml, string Path)? ciYamlResult)
    {
        if (ciYamlResult == null)
        {
            return;
        }

        var (ciYaml, ciYamlPath) = ciYamlResult.Value;
        var parameters = ciYaml.Parameters;
        var repoRoot = info.RepoRoot;
        var ciYamlDir = Path.GetDirectoryName(ciYamlPath) ?? string.Empty;

        // Find the artifact entry for this package
        var artifact = parameters?.Artifacts?
            .FirstOrDefault(a => string.Equals(a.Name, info.ArtifactName, StringComparison.OrdinalIgnoreCase));

        // Collect matrix configs
        var matrixConfigs = new List<Dictionary<string, object?>>();
        AddMatrixConfigs(matrixConfigs, parameters?.MatrixConfigs);
        AddMatrixConfigs(matrixConfigs, parameters?.AdditionalMatrixConfigs);
        info.CiParameters.MatrixConfigs = matrixConfigs;

        // Collect triggering paths
        var triggeringPaths = new List<string>();
        if (artifact?.TriggeringPaths != null)
        {
            triggeringPaths.AddRange(artifact.TriggeringPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
        if (parameters?.TriggeringPaths != null)
        {
            triggeringPaths.AddRange(parameters.TriggeringPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        var ciYamlRelative = GetRelativePath(ciYamlPath, repoRoot);
        if (!string.IsNullOrEmpty(ciYamlRelative))
        {
            triggeringPaths.Add("/" + ciYamlRelative);
        }

        info.TriggeringPaths = ResolveTriggeringPaths(triggeringPaths, ciYamlDir, repoRoot);

        // Additional validation packages
        var additionalPackages = artifact?.AdditionalValidationPackages?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (NormalizedPath)p)
            .ToList() ?? [];

        info.AdditionalValidationPackages = additionalPackages.Count > 0 ? additionalPackages : null;
    }

    private static (ICiPipelineYaml Yaml, string Path)? TryFindCiYaml<TParameters>(PackageInfo info)
        where TParameters : CiPipelineYamlParametersBase
    {
        if (string.IsNullOrWhiteSpace(info.ServiceDirectory))
        {
            return null;
        }

        var repoRoot = info.RepoRoot;
        var sdkCiDir = Path.Combine(repoRoot, "sdk", info.ServiceDirectory);
        var engCiDir = Path.Combine(repoRoot, "eng", info.ServiceDirectory);

        var ciRoot = Directory.Exists(sdkCiDir) ? sdkCiDir : engCiDir;
        if (!Directory.Exists(ciRoot))
        {
            return null;
        }

        var ciFiles = Directory.GetFiles(ciRoot, "ci*.yml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ciFiles.Length == 0)
        {
            return null;
        }

        var isSoleCiYaml = ciFiles.Length == 1;

        foreach (var ciFile in ciFiles)
        {
            var yaml = DeserializeYaml<CiPipelineYaml<TParameters>>(ciFile);
            if (yaml == null)
            {
                continue;
            }

            if (isSoleCiYaml || MatchesArtifact(yaml, info.ArtifactName, info.Group))
            {
                return (yaml, ciFile);
            }
        }

        return null;
    }

    private static TModel? DeserializeYaml<TModel>(string path) where TModel : class
    {
        try
        {
            using var reader = new StreamReader(path);
            return YamlDeserializer.Deserialize<TModel>(reader);
        }
        catch
        {
            return null;
        }
    }

    private static bool MatchesArtifact(ICiPipelineYaml yaml, string? artifactName, string? group)
    {
        var artifacts = yaml.Parameters?.Artifacts;
        if (artifacts == null)
        {
            return false;
        }

        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Name) ||
                !string.Equals(artifact.Name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(group))
            {
                return true;
            }

            if (string.Equals(artifact.GroupId, group, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddMatrixConfigs(List<Dictionary<string, object?>> destination, List<Dictionary<string, object>>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var config in source)
        {
            // Convert Dictionary<string, object> to Dictionary<string, object?>
            var converted = config.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
            destination.Add(converted);
        }
    }

    private static List<NormalizedPath> ResolveTriggeringPaths(List<string> paths, string ciYamlDir, string repoRoot)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var triggerPath in paths)
        {
            if (string.IsNullOrWhiteSpace(triggerPath))
            {
                continue;
            }

            string fullPath;
            if (triggerPath.StartsWith("/"))
            {
                fullPath = Path.Combine(repoRoot, triggerPath.TrimStart('/'));
            }
            else
            {
                fullPath = Path.Combine(ciYamlDir, triggerPath);
            }

            try
            {
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    fullPath = Path.GetFullPath(fullPath);
                }

                var relative = GetRelativePath(fullPath, repoRoot);
                if (!string.IsNullOrEmpty(relative))
                {
                    resolved.Add("/" + relative);
                }
            }
            catch
            {
                // Skip failed path resolution
            }
        }

        return resolved.Select(p => (NormalizedPath)p).ToList();
    }

    private static string GetRelativePath(string fullPath, string repoRoot)
    {
        try
        {
            return Path.GetRelativePath(repoRoot, fullPath).Replace("\\", "/");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public void WritePackageInfoFile(PackageInfo packageInfo, string outputPath, bool addDevVersion)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (addDevVersion)
        {
            packageInfo.DevVersion = packageInfo.PackageVersion;
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(packageInfo, SerializerOptions));
    }

    /// <summary>
    /// Parse a package path.
    /// Expected general structure: &lt;repoRoot&gt;/sdk/&lt;service&gt;/&lt;package&gt; (language-specific depth checks may vary).
    /// </summary>
    /// <param name="realPackagePath">Real (fully resolved) package path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (RepoRoot, RelativePath, FullPath).</returns>
    public async Task<(string RepoRoot, string RelativePath, string FullPath)> ParsePackagePathAsync(string realPackagePath, CancellationToken ct)
    {
        var full = RealPath.GetRealPath(realPackagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(full, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var relativePath = Path.GetRelativePath(sdkRoot, full).TrimStart(Path.DirectorySeparatorChar);
        return (repoRoot, relativePath, full);
    }
}
