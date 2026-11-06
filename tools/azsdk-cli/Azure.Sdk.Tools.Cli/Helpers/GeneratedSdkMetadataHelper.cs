// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class GeneratedSdkMetadataHelper
{
    internal const string ManagementPlane = "management-plane";
    internal const string DataPlane = "data-plane";
    internal const string UnknownPlane = "unknown";

    public static (string Directory, string Pattern) GetMetadataFileLocation(SdkLanguage language) => language switch
    {
        SdkLanguage.Java => ("src/main/resources/META-INF", "*_metadata.json"),
        SdkLanguage.JavaScript or SdkLanguage.DotNet => ("", "metadata.json"),
        SdkLanguage.Python => ("", "_metadata.json"),
        SdkLanguage.Go => ("testdata", "_metadata.json"),
        _ => ("", "")
    };

    public static async Task PopulateApiVersionsAsync(PackageInfo packageInfo, ILogger logger, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        packageInfo.ApiVersions = [];

        var packagePath = Path.GetFullPath(packageInfo.PackagePath);
        var location = GetMetadataFileLocation(packageInfo.Language);
        if (string.IsNullOrEmpty(location.Pattern))
        {
            return;
        }
        var directory = Path.Combine(packagePath, location.Directory.Replace('/', Path.DirectorySeparatorChar));

        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            var versions = new HashSet<PackageApiVersion>();
            foreach (var metadataFile in Directory.GetFiles(directory, location.Pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using var stream = File.OpenRead(metadataFile);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    versions.UnionWith(PackageInfoHelper.ReadApiVersions(document.RootElement));
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"Unable to read SDK API versions from '{metadataFile}': {ex.Message}", ex);
                }
            }

            packageInfo.ApiVersions = [.. versions
                .OrderBy(value => value.Namespace, StringComparer.Ordinal)
                .ThenBy(value => value.Version, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to discover API versions for package at {PackagePath}", packagePath);
        }
    }

    internal static string ProviderNamespace(string main)
    {
        main = Regex.Replace(Regex.Replace(main, @"/\*[\s\S]*?\*/", ""), @"//[^\n]*", "");
        var explicitNamespace = Regex.Match(main, "@armProviderNamespace\\(\\s*\"([\\w.-]+)\"\\s*\\)").Groups[1].Value;
        return explicitNamespace.Length > 0 ? explicitNamespace :
            Regex.Match(main, @"@armProviderNamespace\b(?!\s*\()[\s\S]*?\bnamespace\s+([\w.]+)\s*[;{]").Groups[1].Value;
    }

    internal static bool IsCommitSha(string value) => Regex.IsMatch(value, @"\A[a-f0-9]{40}\z", RegexOptions.IgnoreCase);

    internal static YamlMappingNode ParseYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode map ? map :
            throw new FormatException("Expected one YAML mapping.");
    }

    internal static string Scalar(YamlMappingNode mapping, string name)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(name), out var value) || value is not YamlScalarNode scalar || scalar.Value is null)
        {
            return "";
        }
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.Plain or YamlDotNet.Core.ScalarStyle.Any &&
            Regex.IsMatch(scalar.Value, @"\A(?:null|true|false|~|[-+]?\d+(?:\.\d+)?)\z", RegexOptions.IgnoreCase))
        {
            return "";
        }
        return scalar.Value;
    }

    internal sealed class ReadinessReader(HttpClient http, string token)
    {
        private const int MaxMetadataFiles = 32;
        private const int MaxDirectoryEntries = 1000;
        private const string GenerationLocationFile = "tsp-location.yaml";
        private readonly record struct SdkSnapshot(string Repository, string HeadSha, SdkLanguage Language);
        private sealed record GenerationLocation(string Directory, string CommitSha, string Repository);

        internal async Task<(List<ServiceApiReadinessSource> Sources, string Plane, List<string> Issues)> ReadAsync(
            string prPath, string headRepository, string headSha, SdkLanguage language, CancellationToken ct)
        {
            var changedPackages = await GetChangedPackagesAsync(prPath, language, ct);
            if (changedPackages is null)
            {
                return ([], UnknownPlane, ["The changed-file listing reached GitHub's 3000-file limit; package discovery is incomplete."]);
            }
            return await ReadGenerationSourcesAsync(changedPackages.Value, new SdkSnapshot(headRepository, headSha, language), ct);
        }

        private async Task<(List<ServiceApiReadinessSource> Sources, string Plane, List<string> Issues)> ReadGenerationSourcesAsync(
            (HashSet<string> Packages, HashSet<string> KnownAtHead) changedPackages, SdkSnapshot sdk, CancellationToken ct)
        {
            var (packages, knownAtHead) = changedPackages;
            var issues = new List<string>();
            var sources = new List<ServiceApiReadinessSource>();
            var planes = new HashSet<string>();
            var survivingPackages = 0;
            foreach (var package in packages.Order(StringComparer.Ordinal))
            {
                bool? exists = knownAtHead.Contains(package) ? true : await PackageExistsAtHeadAsync(package, sdk, ct);
                if (exists == false)
                {
                    continue;
                }
                if (exists is null)
                {
                    planes.Add(UnknownPlane);
                    issues.Add($"{package}: SDK package directory could not be established at the checked head.");
                    continue;
                }
                survivingPackages++;
                var (location, plane) = await ReadGenerationLocationAsync(package, sdk, issues, ct);
                planes.Add(plane);
                if (location is null || plane == DataPlane)
                {
                    continue;
                }
                sources.AddRange(await ReadPackageSourcesAsync(package, location, sdk, issues, ct));
            }
            if (planes.Count > 1)
            {
                issues.Add("Mixed SDK API planes require separate checks.");
            }
            if (survivingPackages == 0)
            {
                issues.Add("No currently changed SDK packages remain at the checked head.");
            }
            return (sources, planes.Count == 1 ? planes.Single() : UnknownPlane, issues);
        }

        private async Task<bool?> PackageExistsAtHeadAsync(string package, SdkSnapshot sdk, CancellationToken ct)
        {
            var encoded = string.Join('/', package.Split('/').Select(Uri.EscapeDataString));
            var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", ct);
            return listing switch { null => false, JsonArray => true, _ => null };
        }

        private async Task<(GenerationLocation? Location, string Plane)> ReadGenerationLocationAsync(
            string package, SdkSnapshot sdk, List<string> issues, CancellationToken ct)
        {
            var path = $"{package}/{GenerationLocationFile}";
            var text = await ReadFileAsync(sdk.Repository, sdk.HeadSha, path, ct);
            if (text is null)
            {
                issues.Add($"{package}: missing SDK generation metadata ({path}).");
                return (null, UnknownPlane);
            }
            try
            {
                var metadata = ParseYaml(text);
                var directory = Scalar(metadata, "directory");
                var commit = Scalar(metadata, "commit");
                var repository = NormalizeSpecRepository(Scalar(metadata, "repo"));
                if (!IsSpecificationProjectDirectory(directory) || !IsCommitSha(commit) || repository is null)
                {
                    issues.Add($"{package}: missing or invalid TypeSpec directory, pinned spec commit, or spec repository.");
                    return (null, UnknownPlane);
                }
                return (new GenerationLocation(directory, commit, repository), GetProjectPlane(directory));
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or FormatException)
            {
                issues.Add($"{package}: invalid YAML generation metadata.");
                return (null, UnknownPlane);
            }
        }

        private static bool IsSpecificationProjectDirectory(string directory)
        {
            var segments = directory.Split('/');
            return segments.Length > 1 && segments[0] == "specification" &&
                segments.Skip(1).All(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not ".." && !segment.Contains('\\'));
        }

        private static string GetProjectPlane(string directory)
        {
            var segments = directory.Split('/');
            if (segments.Contains(DataPlane))
            {
                return DataPlane;
            }
            if (segments.Contains("resource-manager") || directory.Contains(".Management", StringComparison.OrdinalIgnoreCase))
            {
                return ManagementPlane;
            }
            return UnknownPlane;
        }

        private async Task<List<string>> ReadMetadataPathsAsync(string package, SdkSnapshot sdk, List<string> issues, CancellationToken ct)
        {
            var location = GetMetadataFileLocation(sdk.Language);
            var directory = location.Directory.Length == 0 ? package : $"{package}/{location.Directory}";
            if (sdk.Language != SdkLanguage.Java)
            {
                return [$"{directory}/{location.Pattern}"];
            }
            var encoded = string.Join('/', directory.Split('/').Select(Uri.EscapeDataString));
            var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", ct);
            if (listing is not null && (listing is not JsonArray entries || entries.Count >= MaxDirectoryEntries))
            {
                issues.Add($"{package}: Java metadata directory listing is invalid or incomplete.");
                return [];
            }
            var paths = (listing as JsonArray ?? []).Where(entry => Text(entry?["type"]) == "file")
                .Select(entry => Text(entry?["name"]))
                .Where(name => name.EndsWith(location.Pattern[1..], StringComparison.Ordinal) && name.IndexOfAny(['/', '\\', '\0']) < 0)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => $"{directory}/{name}").ToList();
            if (paths.Count > MaxMetadataFiles)
            {
                issues.Add($"{package}: Java metadata exceeds the 32-file evidence limit.");
                return [];
            }
            if (paths.Count == 0)
            {
                issues.Add($"{package}: missing SDK generation metadata ({directory}/{location.Pattern}).");
            }
            return paths;
        }

        private async Task<List<ServiceApiReadinessSource>> ReadPackageSourcesAsync(
            string package, GenerationLocation location, SdkSnapshot sdk, List<string> issues, CancellationToken ct)
        {
            var metadataPaths = await ReadMetadataPathsAsync(package, sdk, issues, ct);
            var sources = new List<ServiceApiReadinessSource>();
            string? inferredProvider = null;
            foreach (var metadataPath in metadataPaths)
            {
                var text = await ReadFileAsync(sdk.Repository, sdk.HeadSha, metadataPath, ct);
                if (text is null)
                {
                    issues.Add($"{package}: missing SDK generation metadata ({metadataPath}).");
                    continue;
                }
                var versions = ReadPackageApiVersions(package, text, location, issues);
                foreach (var entry in versions)
                {
                    var provider = entry.Key;
                    if (provider.Length == 0)
                    {
                        inferredProvider ??= ProviderNamespace(await ReadFileAsync(location.Repository, location.CommitSha, $"{location.Directory}/main.tsp", ct) ?? "");
                        provider = inferredProvider;
                    }
                    if (!Regex.IsMatch(provider, @"\A[\w-]+(?:\.[\w-]+)+\z") ||
                        !Regex.IsMatch(entry.Value, @"\A\d{4}-\d{2}-\d{2}(?:-[a-z0-9-]+)?\z", RegexOptions.IgnoreCase))
                    {
                        issues.Add($"{package}: invalid namespace/API-version entry.");
                        continue;
                    }
                    sources.Add(new ServiceApiReadinessSource
                    {
                        PackagePath = package,
                        Configuration = $"{location.Directory}/tspconfig.yaml",
                        CommitSha = location.CommitSha,
                        SpecRepository = location.Repository,
                        ExpectedNamespace = provider,
                        ApiVersion = entry.Value,
                        MetadataFiles = [$"{package}/{GenerationLocationFile}", .. metadataPaths]
                    });
                }
            }
            if (sources.GroupBy(source => source.ExpectedNamespace, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Select(source => source.ApiVersion).Distinct(StringComparer.Ordinal).Count() > 1))
            {
                issues.Add($"{package}: conflicting API versions across SDK metadata files; no latest-version selection.");
                return [];
            }
            return sources.DistinctBy(source => source.ExpectedNamespace, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static Dictionary<string, string> ReadPackageApiVersions(string package, string text, GenerationLocation location, List<string> issues)
        {
            try
            {
                if (JsonNode.Parse(text) is not JsonObject metadata)
                {
                    issues.Add($"{package}: invalid SDK API-version metadata.");
                    return [];
                }
                if (metadata["commit"] is not null && Text(metadata["commit"]) != location.CommitSha ||
                    metadata["typespec_src"] is not null && Text(metadata["typespec_src"]) != location.Directory ||
                    metadata["repository_url"] is not null && NormalizeSpecRepository(Text(metadata["repository_url"])) != location.Repository)
                {
                    issues.Add($"{package}: SDK metadata conflicts with {GenerationLocationFile}.");
                    return [];
                }
                var versions = PackageInfoHelper.ReadApiVersionsByProvider(metadata);
                if (versions.Count == 0)
                {
                    issues.Add($"{package}: missing or invalid API-version metadata (apiVersions/apiVersion).");
                }
                return versions;
            }
            catch (InvalidOperationException)
            {
                issues.Add($"{package}: conflicting API versions; no latest-version selection.");
            }
            catch (Exception exception) when (exception is JsonException or FormatException)
            {
                issues.Add($"{package}: invalid JSON generation metadata.");
            }
            return [];
        }

        private async Task<(HashSet<string> Packages, HashSet<string> KnownAtHead)?> GetChangedPackagesAsync(
            string prPath, SdkLanguage language, CancellationToken ct)
        {
            const int pageSize = 100;
            const int maxPages = 30;
            var packages = new HashSet<string>(StringComparer.Ordinal);
            var knownAtHead = new HashSet<string>(StringComparer.Ordinal);
            for (var page = 1; page <= maxPages; page++)
            {
                var files = await ReadGitHubAsync($"{prPath}/files?per_page={pageSize}&page={page}", ct) as JsonArray
                    ?? throw new InvalidOperationException("Changed SDK files are unavailable.");
                foreach (var file in files)
                {
                    var filename = Text(file?["filename"]);
                    var status = Text(file?["status"]);
                    if (GetPackagePath(filename, language) is { } package)
                    {
                        packages.Add(package);
                        if (status != "removed")
                        {
                            knownAtHead.Add(package);
                        }
                    }
                    if (status == "renamed" && GetPackagePath(Text(file?["previous_filename"]), language) is { } previousPackage)
                    {
                        packages.Add(previousPackage);
                    }
                }
                if (files.Count < pageSize)
                {
                    return (packages, knownAtHead);
                }
            }
            return null;
        }

        private static string? GetPackagePath(string changedFile, SdkLanguage language)
        {
            var segments = changedFile.Split('/');
            var packageDepth = language == SdkLanguage.Go ? 4 : 3;
            if (segments.Length <= packageDepth || segments[0] != "sdk" || segments[1] == "test" ||
                segments.Any(segment => segment is "" or "." or ".." || segment.Contains('\\')) ||
                language == SdkLanguage.Go && segments[1] != "resourcemanager")
            {
                return null;
            }
            return string.Join('/', segments.Take(packageDepth));
        }

        private static string? NormalizeSpecRepository(string value)
        {
            var repository = value.TrimEnd('/').Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase);
            if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                repository = repository[..^4];
            }
            return repository.Equals("Azure/azure-rest-api-specs", StringComparison.OrdinalIgnoreCase) ? "Azure/azure-rest-api-specs" :
                repository.Equals("Azure/azure-rest-api-specs-pr", StringComparison.OrdinalIgnoreCase) ? "Azure/azure-rest-api-specs-pr" : null;
        }

        private Task<JsonNode?> ReadGitHubAsync(string path, CancellationToken ct) =>
            GitHubService.ReadJsonEvidenceAsync(http, path, token, ct);

        private Task<string?> ReadFileAsync(string repository, string commit, string path, CancellationToken ct) =>
            GitHubService.ReadFileEvidenceAsync(http, repository, commit, path, token, ct);

        private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
    }
}
