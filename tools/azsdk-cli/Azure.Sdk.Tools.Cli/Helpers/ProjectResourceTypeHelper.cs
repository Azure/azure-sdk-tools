using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using YamlDotNet.RepresentationModel;
using static Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelper;

namespace Azure.Sdk.Tools.Cli.Helpers;

// Map checked-in spec files at the recorded commit without compiling TypeSpec.
public sealed class ProjectResourceTypeHelper(HttpClient http, string token)
{
    private const int MaxDirectoryEntries = 1000;

    public sealed record ResourceTypeEvidence(PackageResourceType[] ResourceTypes, string[] ProviderNamespaces, List<string> MissingEvidence);
    private sealed record ProjectSource(string SpecRepository, string CommitSha);

    public async Task<ResourceTypeEvidence> ReadPinnedSwaggerResourceTypesAsync(
        TypeSpecLocation location, YamlMappingNode? configuration, string apiVersion, CancellationToken ct = default)
    {
        if (location.Repository is not ("Azure/azure-rest-api-specs" or "Azure/azure-rest-api-specs-pr") ||
            !GeneratedSdkMetadataHelper.IsCommitSha(location.Commit) ||
            !location.Directory.StartsWith("specification/", StringComparison.Ordinal) ||
            location.Directory.Split('/').Any(part => part is "" or "." or ".." || part.Contains('\\')))
        {
            throw new ArgumentException("Provide a pinned Azure specification project configuration.");
        }
        var source = new ProjectSource(location.Repository, location.Commit);
        if (configuration is null)
        {
            return new([], [], ["TypeSpec configuration at spec commit"]);
        }
        var swaggerPattern = SwaggerOutputPattern(configuration, location.Directory, apiVersion);
        if (swaggerPattern is null)
        {
            return new([], [], ["checked-in Swagger output mapping"]);
        }
        var resourceTypes = new HashSet<PackageResourceType>();
        var providerNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingEvidence = new List<string>();
        var swaggerFiles = await DiscoverSwaggerFilesAsync(swaggerPattern, source, ct);
        foreach (var swaggerFile in swaggerFiles)
        {
            var swaggerText = await ReadFileAsync(source, swaggerFile, ct);
            if (swaggerText is not null)
            {
                AddSwaggerResourceTypes(swaggerText, swaggerFile, apiVersion, resourceTypes, providerNamespaces, missingEvidence);
            }
            else
            {
                missingEvidence.Add($"Missing checked-in Swagger: {swaggerFile}");
            }
        }
        if (resourceTypes.Count == 0)
        {
            missingEvidence.Add("resource-type mapping");
        }
        return new([.. resourceTypes.OrderBy(type => type.Namespace, StringComparer.Ordinal)
            .ThenBy(type => type.ApiVersion, StringComparer.Ordinal).ThenBy(type => type.ResourceType, StringComparer.Ordinal)],
            [.. providerNamespaces.Order(StringComparer.Ordinal)], missingEvidence);
    }

    private async Task<List<string>> DiscoverSwaggerFilesAsync(string pattern, ProjectSource source, CancellationToken ct)
    {
        var parts = pattern.Split('/');
        var files = new List<string>();
        await DiscoverAsync(0, "");
        return files;

        async Task DiscoverAsync(int index, string directory)
        {
            var wildcard = Array.FindIndex(parts, index, part => part.Contains('*'));
            if (wildcard < 0)
            {
                files.Add(string.Join('/', new[] { directory }.Concat(parts.Skip(index)).Where(part => part.Length > 0)));
                return;
            }
            var parent = string.Join('/', new[] { directory }.Concat(parts.Skip(index).Take(wildcard - index)).Where(part => part.Length > 0));
            var encoded = string.Join('/', parent.Split('/').Select(Uri.EscapeDataString));
            var listing = await GitHubService.ReadJsonEvidenceAsync(http,
                $"repos/{source.SpecRepository}/contents/{encoded}?ref={source.CommitSha}", token, ct);
            if (listing is null)
            {
                return;
            }
            if (listing is not JsonArray entries || entries.Count >= MaxDirectoryEntries)
            {
                throw new InvalidOperationException("Swagger directory evidence is invalid or incomplete.");
            }
            var matcher = $"\\A{Regex.Escape(parts[wildcard]).Replace("\\*", "[^/]*", StringComparison.Ordinal)}\\z";
            foreach (var entry in entries)
            {
                var name = ReadJsonString(entry?["name"]);
                if (!Regex.IsMatch(name, matcher) || name is "." or ".." || name.Contains('/') || name.Contains('\\'))
                {
                    continue;
                }
                var path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
                if (wildcard == parts.Length - 1 && ReadJsonString(entry?["type"]) == "file")
                {
                    files.Add(path);
                }
                else if (ReadJsonString(entry?["type"]) == "dir")
                {
                    await DiscoverAsync(wildcard + 1, path);
                }
            }
        }
    }

    private static void AddSwaggerResourceTypes(string text, string file, string apiVersion,
        HashSet<PackageResourceType> types, HashSet<string> providers, List<string> missing)
    {
        JsonObject? document;
        try
        {
            document = JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            document = null;
        }
        if (document is null)
        {
            missing.Add($"Invalid checked-in Swagger: {file}");
            return;
        }
        if (document["info"] is not JsonObject info || ReadJsonString(info["version"]) != apiVersion)
        {
            missing.Add($"Swagger API version does not match SDK generation metadata: {file}");
            return;
        }
        if (document["paths"] is not JsonObject || document["x-ms-paths"] is { } extendedPaths && extendedPaths is not JsonObject)
        {
            missing.Add($"Invalid checked-in Swagger paths: {file}");
            return;
        }
        foreach (var paths in new[] { document["paths"], document["x-ms-paths"] }.OfType<JsonObject>())
        {
            foreach (var route in paths)
            {
                var collection = route.Value is JsonObject operations && operations.ContainsKey("get");
                AddResourceTypesFromPath(route.Key, apiVersion, collection, types, providers, missing);
            }
        }
    }

    private static string? SwaggerOutputPattern(YamlMappingNode configuration, string project, string apiVersion)
    {
        // Inherited emitter options are unresolved here; do not guess their output mapping.
        if (configuration.Children.ContainsKey(new YamlScalarNode("extends")) ||
            !configuration.Children.TryGetValue(new YamlScalarNode("options"), out var optionsNode) || optionsNode is not YamlMappingNode options ||
            !options.Children.TryGetValue(new YamlScalarNode("@azure-tools/typespec-autorest"), out var emitterNode) || emitterNode is not YamlMappingNode emitter)
        {
            return null;
        }
        return ResolveOutputPattern(configuration, emitter, project, apiVersion);
    }

    private static string? ResolveOutputPattern(YamlMappingNode config, YamlMappingNode emitter, string project, string apiVersion)
    {
        var outputDirectory = config.Children.ContainsKey(new YamlScalarNode("output-dir"))
            ? GeneratedSdkMetadataHelper.Scalar(config, "output-dir") : "{project-root}/tsp-output";
        var emitterDirectory = GeneratedSdkMetadataHelper.Scalar(emitter, "emitter-output-dir");
        var outputFile = GeneratedSdkMetadataHelper.Scalar(emitter, "output-file");
        var providerFolder = GeneratedSdkMetadataHelper.Scalar(emitter, "azure-resource-provider-folder");
        if (outputDirectory.Length == 0 || emitterDirectory.Length == 0 || outputFile.Length == 0)
        {
            return null;
        }
        if (providerFolder.Length > 0)
        {
            outputDirectory = outputDirectory.Replace("{azure-resource-provider-folder}", providerFolder, StringComparison.Ordinal);
            emitterDirectory = emitterDirectory.Replace("{azure-resource-provider-folder}", providerFolder, StringComparison.Ordinal);
            outputFile = outputFile.Replace("{azure-resource-provider-folder}", providerFolder, StringComparison.Ordinal);
        }
        outputDirectory = outputDirectory.Replace("{project-root}", project, StringComparison.Ordinal);
        emitterDirectory = emitterDirectory.Replace("{project-root}", project, StringComparison.Ordinal)
            .Replace("{output-dir}", outputDirectory, StringComparison.Ordinal);
        outputFile = outputFile.Replace("{project-root}", project, StringComparison.Ordinal)
            .Replace("{emitter-output-dir}", emitterDirectory, StringComparison.Ordinal)
            .Replace("{output-dir}", outputDirectory, StringComparison.Ordinal)
            .Replace("{version-status}", apiVersion.Contains("preview", StringComparison.OrdinalIgnoreCase) ? "preview" : "stable", StringComparison.Ordinal)
            .Replace("{version}", apiVersion, StringComparison.Ordinal);
        outputFile = Regex.Replace(outputFile, @"\{[^}]+\}", "*");
        var candidate = outputFile.StartsWith("specification/", StringComparison.Ordinal) ? outputFile : $"{emitterDirectory}/{outputFile}";
        return NormalizeOutputPath(candidate);
    }

    private static string? NormalizeOutputPath(string candidate)
    {
        if (candidate.Contains('\\'))
        {
            return null;
        }
        var normalized = new List<string>();
        foreach (var part in candidate.Split('/'))
        {
            if (part is "" or ".")
            {
                continue;
            }
            if (part == "..")
            {
                if (normalized.Count == 0)
                {
                    return null;
                }
                normalized.RemoveAt(normalized.Count - 1);
            }
            else
            {
                normalized.Add(part);
            }
        }
        var pattern = string.Join('/', normalized);
        return pattern.StartsWith("specification/", StringComparison.Ordinal) && pattern.EndsWith(".json", StringComparison.Ordinal) ? pattern : null;
    }

    private static void AddResourceTypesFromPath(string path, string apiVersion, bool collection,
        HashSet<PackageResourceType> types, HashSet<string> providers, List<string> missing)
    {
        var segments = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindLastIndex(segments, segment => segment.Equals("providers", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }
        if (index + 1 >= segments.Length || !Regex.IsMatch(segments[index + 1], @"\A[\w-]+(?:\.[\w-]+)+\z"))
        {
            missing.Add($"Unresolved resource-provider mapping: {path}");
            return;
        }
        var providerNamespace = segments[index + 1];
        providers.Add(providerNamespace);
        var type = ReadResourceType(segments, index + 2, collection);
        if (type is not null)
        {
            types.Add(new(providerNamespace, apiVersion, type));
        }
    }

    private static string? ReadResourceType(string[] segments, int start, bool collection)
    {
        var types = new List<string>();
        for (var cursor = start; cursor < segments.Length; cursor += 2)
        {
            var type = segments[cursor];
            if (!Regex.IsMatch(type, @"\A[\w.-]+\z") || type.Equals("operations", StringComparison.OrdinalIgnoreCase) || type.Equals("providers", StringComparison.OrdinalIgnoreCase))
            {
                types.Clear();
                break;
            }
            var name = cursor + 1 < segments.Length ? segments[cursor + 1] : null;
            if (name is null && !collection)
            {
                break;
            }
            // Named resources use placeholders; "default" also supports singleton resources.
            if (name is not null && !Regex.IsMatch(name, @"\A\{[^}]+\}\z") && name != "default")
            {
                types.Clear();
                break;
            }
            types.Add(type);
        }
        return types.Count > 0 ? string.Join('/', types) : null;
    }

    private Task<string?> ReadFileAsync(ProjectSource source, string path, CancellationToken ct) =>
        GitHubService.ReadFileEvidenceAsync(http, source.SpecRepository, source.CommitSha, path, token, ct);

}