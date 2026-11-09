using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Helpers;

// Map checked-in spec files at the recorded commit without compiling TypeSpec.
public sealed class ProjectResourceTypeHelper(HttpClient http, string token)
{
    private const int MaxSwaggerFiles = 32;
    private const int MaxSwaggerDirectories = 64;
    private const int MaxDirectoryEntries = 1000;
    private const string ConfigurationSuffix = "/tspconfig.yaml";

    public sealed record ResourceTypeEvidence(PackageResourceType[] ResourceTypes, List<string> MissingEvidence);
    private sealed record SpecMapping(string Namespace, string? SwaggerPattern, List<string> MissingEvidence);
    private sealed record ProjectSource(string SpecRepository, string CommitSha, string Configuration, string ApiVersion);

    public async Task<ResourceTypeEvidence> ReadPinnedSwaggerResourceTypesAsync(
        string specRepository, string commitSha, string configuration, string apiVersion, CancellationToken ct = default)
    {
        if (specRepository is not ("Azure/azure-rest-api-specs" or "Azure/azure-rest-api-specs-pr") ||
            !GeneratedSdkMetadataHelper.IsCommitSha(commitSha) ||
            !configuration.StartsWith("specification/", StringComparison.Ordinal) ||
            !configuration.EndsWith(ConfigurationSuffix, StringComparison.Ordinal) ||
            configuration.Split('/').Any(part => part is "" or "." or ".." || part.Contains('\\')))
        {
            throw new ArgumentException("Provide a pinned Azure specification project configuration.");
        }
        var source = new ProjectSource(specRepository, commitSha, configuration, apiVersion);
        var mapping = await ReadPinnedSwaggerMappingAsync(source, ct);
        var types = new HashSet<PackageResourceType>();
        if (mapping.SwaggerPattern is null)
        {
            return new([], mapping.MissingEvidence);
        }
        var files = await DiscoverSwaggerFilesAsync(mapping.SwaggerPattern, source, ct);
        foreach (var file in files)
        {
            var text = await ReadFileAsync(source, file, ct);
            if (text is not null)
            {
                AddSwaggerResourceTypes(text, file, source.ApiVersion, types, mapping.MissingEvidence);
            }
        }
        if (types.Count == 0)
        {
            mapping.MissingEvidence.Add("resource-type mapping");
        }
        return new([.. types.OrderBy(type => type.Namespace, StringComparer.Ordinal)
            .ThenBy(type => type.ApiVersion, StringComparer.Ordinal).ThenBy(type => type.ResourceType, StringComparer.Ordinal)], mapping.MissingEvidence);
    }

    private async Task<SpecMapping> ReadPinnedSwaggerMappingAsync(
        ProjectSource source, CancellationToken ct)
    {
        var project = source.Configuration[..^ConfigurationSuffix.Length];
        var configTask = ReadFileAsync(source, source.Configuration, ct);
        var mainTask = ReadFileAsync(source, $"{project}/main.tsp", ct);
        await Task.WhenAll(configTask, mainTask);
        var configText = await configTask;
        var provider = GeneratedSdkMetadataHelper.ProviderNamespace(await mainTask ?? "");
        var missing = new List<string>();
        if (string.IsNullOrEmpty(provider))
        {
            missing.Add("provider namespace");
        }
        if (configText is null)
        {
            missing.Add("TypeSpec configuration at spec commit");
        }
        if (missing.Count > 0)
        {
            return new(provider, null, missing);
        }
        var pattern = SwaggerOutputPattern(configText!, project, source.ApiVersion);
        if (pattern is null)
        {
            missing.Add("checked-in Swagger output mapping");
        }
        return new(provider, pattern, missing);
    }

    private async Task<List<string>> DiscoverSwaggerFilesAsync(string pattern, ProjectSource source, CancellationToken ct)
    {
        var parts = pattern.Split('/');
        var files = new List<string>();
        var visited = 0;
        await DiscoverAsync(0, "");
        return files;

        async Task DiscoverAsync(int index, string directory)
        {
            if (++visited > MaxSwaggerDirectories || files.Count >= MaxSwaggerFiles)
            {
                throw new InvalidOperationException("Swagger discovery exceeded the per-project evidence limit.");
            }
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
                var name = Text(entry?["name"]);
                if (!Regex.IsMatch(name, matcher) || name is "." or ".." || name.Contains('/') || name.Contains('\\'))
                {
                    continue;
                }
                var path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
                if (wildcard == parts.Length - 1 && Text(entry?["type"]) == "file")
                {
                    files.Add(path);
                    if (files.Count > MaxSwaggerFiles)
                    {
                        throw new InvalidOperationException("Too many Swagger files for one project check.");
                    }
                }
                else if (Text(entry?["type"]) == "dir")
                {
                    await DiscoverAsync(wildcard + 1, path);
                }
            }
        }
    }

    private static void AddSwaggerResourceTypes(string text, string file, string apiVersion,
        HashSet<PackageResourceType> types, List<string> missing)
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
        if (document["info"] is not JsonObject info || Text(info["version"]) != apiVersion)
        {
            missing.Add($"Swagger API version does not match SDK generation metadata: {file}");
            return;
        }
        foreach (var paths in new[] { document["paths"], document["x-ms-paths"] }.OfType<JsonObject>())
        {
            foreach (var route in paths)
            {
                var collection = route.Value is JsonObject operations && operations.ContainsKey("get");
                types.UnionWith(ResourceTypesFromPath(route.Key, apiVersion, collection));
            }
        }
    }

    private static string? SwaggerOutputPattern(string text, string project, string apiVersion)
    {
        try
        {
            var config = GeneratedSdkMetadataHelper.ParseYaml(text);
            // Inherited emitter options are unresolved here; do not guess their output mapping.
            if (config.Children.ContainsKey(new YamlScalarNode("extends")) ||
                !config.Children.TryGetValue(new YamlScalarNode("options"), out var optionsNode) || optionsNode is not YamlMappingNode options ||
                !options.Children.TryGetValue(new YamlScalarNode("@azure-tools/typespec-autorest"), out var emitterNode) || emitterNode is not YamlMappingNode emitter)
            {
                return null;
            }
            return ResolveOutputPattern(config, emitter, project, apiVersion);
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or FormatException)
        {
            return null;
        }
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

    private static IEnumerable<PackageResourceType> ResourceTypesFromPath(string path, string apiVersion, bool collection)
    {
        var segments = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!segments[index].Equals("providers", StringComparison.OrdinalIgnoreCase) ||
                !Regex.IsMatch(segments[index + 1], @"\A[\w-]+(?:\.[\w-]+)+\z"))
            {
                continue;
            }
            var type = ReadResourceType(segments, index + 2, collection);
            if (type is not null)
            {
                yield return new(segments[index + 1], apiVersion, type);
            }
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

    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
}