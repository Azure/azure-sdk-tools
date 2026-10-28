using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IServiceApiReadinessService
{
    Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, string? expectedHeadSha, CancellationToken ct);
}

public class ServiceApiReadinessService(IGitHubService github, IAzureService azure, IHttpClientFactory clients) : IServiceApiReadinessService
{
    public const string HttpClientName = "ServiceApiReadiness";
    public const string ArmMetadataApiVersion = "2021-04-01";
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    public async Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, string? expectedHeadSha, CancellationToken ct)
    {
        var result = new ServiceApiReadinessResponse { HeadSha = expectedHeadSha ?? "" };
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(40));
        ct = budget.Token;
        try
        {
            if (!Regex.IsMatch(owner, @"\A[\w.-]+\z") ||
                !Regex.IsMatch(repository, @"\Aazure-sdk-for-(net|java|js|python|go)\z", RegexOptions.IgnoreCase) ||
                pullRequest <= 0 || (expectedHeadSha is not null && !IsSha(expectedHeadSha)))
            {
                throw new ArgumentException("Provide an SDK repository, positive PR number, and valid expected head SHA.");
            }
            var token = github.GetAuthToken();
            var prPath = $"repos/{owner}/{repository}/pulls/{pullRequest}";
            var pr = await ReadGitHubAsync(prPath, token, ct) ?? throw new InvalidOperationException("SDK PR is unavailable.");
            result.HeadSha = Text(pr["head"]?["sha"]);
            result.HeadRepository = Text(pr["head"]?["repo"]?["full_name"]);
            if (Text(pr["state"]) != "open" || !IsSha(result.HeadSha) ||
                !Regex.IsMatch(result.HeadRepository, @"\A[\w.-]+/[\w.-]+\z"))
            {
                result.Detail = "An open SDK PR with a current head repository and full head SHA is required.";
                return result;
            }
            if (expectedHeadSha is not null && !string.Equals(expectedHeadSha, result.HeadSha, StringComparison.OrdinalIgnoreCase))
            {
                result.Detail = "The SDK PR head changed before the check. Run it against the current head.";
                return result;
            }
            var labels = (pr["labels"] as JsonArray ?? []).Select(label => Text(label?["name"])).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var managementLabel = labels.Contains("resource-manager");
            var dataLabel = labels.Contains("data-plane");
            var issues = new List<string>();
            if (managementLabel && dataLabel)
            {
                result.Detail = "Conflicting resource-manager and data-plane labels.";
                return result;
            }
            result.Plane = managementLabel ? "management-plane" : dataLabel ? "data-plane" : "unknown";
            var packages = new HashSet<string>(StringComparer.Ordinal);
            for (var page = 1; page <= 30; page++)
            {
                var files = await ReadGitHubAsync($"{prPath}/files?per_page=100&page={page}", token, ct) as JsonArray
                    ?? throw new InvalidOperationException("Changed SDK files are unavailable.");
                foreach (var file in files)
                {
                    var parts = Text(file?["filename"]).Split('/');
                    var length = repository.Equals("azure-sdk-for-go", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
                    if (Text(file?["status"]) == "removed" || parts.Length <= length || parts[0] != "sdk" || parts[1] == "test" ||
                        parts.Any(part => part is "" or "." or ".." || part.Contains('\\')) || (length == 4 && parts[1] != "resourcemanager"))
                    {
                        continue;
                    }
                    packages.Add(string.Join('/', parts.Take(length)));
                }
                if (files.Count < 100)
                {
                    break;
                }
                if (page == 30)
                {
                    result.Detail = "The changed-file listing reached GitHub's 3000-file limit; package discovery is incomplete.";
                    return result;
                }
            }
            var sources = new List<ServiceApiReadinessSource>();
            var planes = new HashSet<string>();
            foreach (var package in packages.Order(StringComparer.Ordinal))
            {
                var basename = package.Split('/')[^1];
                var language = repository["azure-sdk-for-".Length..].ToLowerInvariant();
                var metadataFile = language switch
                {
                    "python" => "_metadata.json",
                    "go" => "testdata/_metadata.json",
                    "java" => $"src/main/resources/META-INF/{basename}_metadata.json",
                    _ => "metadata.json"
                };
                var locationPath = $"{package}/tsp-location.yaml";
                var metadataPath = $"{package}/{metadataFile}";
                var locationText = await ReadFileAsync(result.HeadRepository, result.HeadSha, locationPath, token, ct);
                var metadataText = await ReadFileAsync(result.HeadRepository, result.HeadSha, metadataPath, token, ct);
                if (locationText is null || metadataText is null)
                {
                    issues.Add($"{package}: missing SDK generation metadata ({(locationText is null ? locationPath : metadataPath)}).");
                    continue;
                }
                try
                {
                    var location = ParseYaml(locationText);
                    var directory = Scalar(location, "directory");
                    var commit = Scalar(location, "commit");
                    var specRepository = NormalizeSpecRepository(Scalar(location, "repo"));
                    var plane = directory.Split('/').Contains("data-plane") ? "data-plane" :
                        directory.Split('/').Contains("resource-manager") ? "management-plane" : result.Plane;
                    planes.Add(plane);
                    if (!Regex.IsMatch(directory, @"\Aspecification/[\w.-]+(?:/[\w.-]+)*\z") ||
                        directory.Split('/').Any(part => part is "." or "..") || Regex.IsMatch(directory, @"\.(tsp|yaml|yml|json)\z") ||
                        !IsSha(commit) || specRepository is null)
                    {
                        issues.Add($"{package}: missing or invalid TypeSpec directory, pinned spec commit, or spec repository.");
                        continue;
                    }
                    if (JsonNode.Parse(metadataText) is not JsonObject metadata)
                    {
                        issues.Add($"{package}: invalid SDK API-version metadata.");
                        continue;
                    }
                    if (metadata["commit"] is not null && Text(metadata["commit"]) != commit ||
                        metadata["typespec_src"] is not null && Text(metadata["typespec_src"]) != directory ||
                        metadata["repository_url"] is not null && NormalizeSpecRepository(Text(metadata["repository_url"])) != specRepository)
                    {
                        issues.Add($"{package}: SDK metadata conflicts with tsp-location.yaml.");
                        continue;
                    }
                    var parserMetadata = metadata.DeepClone().AsObject();
                    var ambiguous = false;
                    if (parserMetadata["apiVersions"] is JsonObject versionMap)
                    {
                        foreach (var entry in versionMap.ToArray())
                        {
                            if (entry.Value is not JsonArray array)
                            {
                                continue;
                            }
                            var distinct = array.Select(Text).Distinct(StringComparer.Ordinal).ToArray();
                            if (distinct.Length != 1 || string.IsNullOrWhiteSpace(distinct[0]))
                            {
                                ambiguous = true;
                                continue;
                            }
                            versionMap[entry.Key] = distinct[0];
                        }
                    }
                    if (ambiguous)
                    {
                        issues.Add($"{package}: conflicting API versions; no latest-version selection.");
                        continue;
                    }
                    var versions = GeneratedSdkMetadataHelper.ReadApiVersions(JsonSerializer.SerializeToElement(parserMetadata));
                    if (versions.Count == 0)
                    {
                        issues.Add($"{package}: missing or invalid API-version metadata (apiVersions/apiVersion).");
                        continue;
                    }
                    foreach (var entry in versions.GroupBy(version => version.Namespace ?? "", StringComparer.Ordinal))
                    {
                        var expectedNamespace = entry.Key;
                        if (expectedNamespace.Length == 0)
                        {
                            expectedNamespace = ProviderNamespace(await ReadFileAsync(specRepository, commit, $"{directory}/main.tsp", token, ct) ?? "");
                        }
                        var candidates = entry.Select(version => version.Version).Distinct(StringComparer.Ordinal).ToArray();
                        if (!Regex.IsMatch(expectedNamespace, @"\A[\w-]+(?:\.[\w-]+)+\z") || candidates.Length == 0 ||
                            candidates.Any(version => !Regex.IsMatch(version, @"\A\d{4}-\d{2}-\d{2}(?:-[a-z0-9-]+)?\z", RegexOptions.IgnoreCase)))
                        {
                            issues.Add($"{package}: invalid namespace/API-version entry.");
                            continue;
                        }
                        if (candidates.Distinct(StringComparer.Ordinal).Count() != 1 ||
                            metadata["apiVersion"] is not null && Text(metadata["apiVersion"]) != candidates[0])
                        {
                            issues.Add($"{package}: conflicting API versions; no latest-version selection.");
                            continue;
                        }
                        sources.Add(new ServiceApiReadinessSource
                        {
                            PackagePath = package,
                            Configuration = $"{directory}/tspconfig.yaml",
                            CommitSha = commit,
                            SpecRepository = specRepository,
                            ExpectedNamespace = expectedNamespace,
                            ApiVersion = candidates[0],
                            MetadataFiles = [locationPath, metadataPath]
                        });
                    }
                }
                catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
                {
                    issues.Add($"{package}: invalid YAML or JSON generation metadata.");
                }
            }
            if (planes.Count == 1)
            {
                result.Plane = planes.Single();
            }
            else if (planes.Count > 1)
            {
                result.Plane = "unknown";
                issues.Add("Mixed SDK API planes require separate checks.");
            }
            if (result.Plane == "data-plane")
            {
                result.Detail = "Data-plane SDK; ARM provider metadata is not applicable.";
                return result;
            }
            if (packages.Count == 0)
            {
                issues.Add("No currently changed SDK packages were found.");
            }
            if (issues.Count > 0 || sources.Count == 0 || result.Plane == "unknown")
            {
                result.Detail = issues.Count > 0 ? string.Join(" ", issues) : "Authoritative SDK generation metadata or API plane is unavailable.";
                return result;
            }
            var providers = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
            TokenCredential? credential = null;
            foreach (var source in sources)
            {
                ct.ThrowIfCancellationRequested();
                var project = new ServiceApiReadinessProject { Source = source };
                result.Projects.Add(project);
                try
                {
                    var spec = await ReadSpecAsync(source, token, ct);
                    project.Namespace = spec.Namespace;
                    if (spec.Missing.Count > 0)
                    {
                        project.Detail = $"Missing or unresolved: {string.Join(", ", spec.Missing)}.";
                        continue;
                    }
                    if (!spec.Namespace.Equals(source.ExpectedNamespace, StringComparison.OrdinalIgnoreCase))
                    {
                        project.Detail = "SDK metadata namespace conflicts with the pinned spec provider namespace.";
                        continue;
                    }
                    if (!providers.TryGetValue(spec.Namespace, out var provider))
                    {
                        credential ??= azure.GetCredential();
                        provider = await ReadProviderAsync(spec.Namespace, credential, ct);
                        providers.Add(spec.Namespace, provider);
                    }
                    if (provider is null)
                    {
                        project.Detail = "The provider is unavailable in the validation tenant.";
                        continue;
                    }
                    if (!Text(provider["namespace"]).Equals(spec.Namespace, StringComparison.OrdinalIgnoreCase) ||
                        provider["resourceTypes"] is not JsonArray resourceTypes)
                    {
                        throw new InvalidOperationException("Unexpected ARM provider metadata response.");
                    }
                    var applicable = resourceTypes.Where(resource => spec.Types.Contains(Text(resource?["resourceType"]))).ToArray();
                    if (applicable.Length == 0)
                    {
                        project.Detail = "No project resource type could be mapped to provider metadata.";
                        continue;
                    }
                    foreach (var resource in applicable)
                    {
                        if (resource?["apiVersions"] is not JsonArray advertised || !advertised.Select(Text).Contains(source.ApiVersion, StringComparer.Ordinal))
                        {
                            continue;
                        }
                        var regions = (resource["locations"] as JsonArray ?? []).Select(Text)
                            .Concat((resource["locationMappings"] as JsonArray ?? []).Select(mapping => Text(mapping?["location"])))
                            .Where(region => !string.IsNullOrEmpty(region)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                        project.Matches.Add(new ServiceApiReadinessMatch { ResourceType = Text(resource["resourceType"]), Regions = regions });
                    }
                    project.ReadinessStatus = project.Matches.Count > 0 ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected;
                    project.Detail = $"{project.Matches.Count} of {applicable.Length} mapped resource types advertise the exact version ({spec.Types.Count} project resource types identified).";
                    if (project.ReadinessStatus == ServiceApiReadinessResponse.NotDetected)
                    {
                        project.Detail += " Ask the service owner to confirm deployment; this is not a confirmed deployment failure.";
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    project.ReadinessStatus = ServiceApiReadinessResponse.Error;
                    project.Detail = SafeError(exception);
                }
            }
            result.ReadinessStatus = result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error) ? ServiceApiReadinessResponse.Error :
                result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Inconclusive) ? ServiceApiReadinessResponse.Inconclusive :
                result.Projects.All(project => project.ReadinessStatus == ServiceApiReadinessResponse.Passed) ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected;
            result.Detail = "API versions were read from SDK metadata files at the checked PR head. Provider metadata is evidence, not release approval or proof of regional deployment.";
            var current = await ReadGitHubAsync(prPath, token, ct);
            if (Text(current?["head"]?["sha"]) != result.HeadSha || Text(current?["state"]) != "open")
            {
                result.ReadinessStatus = ServiceApiReadinessResponse.Inconclusive;
                result.Detail = "The SDK PR changed during the check; run it against its current head.";
            }
            if (result.ReadinessStatus == ServiceApiReadinessResponse.Error)
            {
                result.ResponseError = "A platform or application failure prevented the readiness check.";
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Error;
            result.Detail = exception is ArgumentException ? "Provide an SDK repository, positive PR number, and valid expected head SHA." : SafeError(exception);
            result.ResponseError = result.Detail;
        }
        return result;
    }

    private async Task<JsonNode?> ReadGitHubAsync(string path, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("azsdk-service-api-readiness");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        if (text.Length > 4 * 1024 * 1024)
        {
            throw new InvalidOperationException("GitHub response exceeded the evidence size limit.");
        }
        return JsonNode.Parse(text);
    }

    private async Task<JsonNode?> ReadProviderAsync(string providerNamespace, TokenCredential credential, CancellationToken ct)
    {
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com/providers/{Uri.EscapeDataString(providerNamespace)}?api-version={ArmMetadataApiVersion}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<(string Namespace, HashSet<string> Types, List<string> Missing)> ReadSpecAsync(ServiceApiReadinessSource source, string token, CancellationToken ct)
    {
        var project = source.Configuration[..^"/tspconfig.yaml".Length];
        var configTask = ReadFileAsync(source.SpecRepository, source.CommitSha, source.Configuration, token, ct);
        var mainTask = ReadFileAsync(source.SpecRepository, source.CommitSha, $"{project}/main.tsp", token, ct);
        await Task.WhenAll(configTask, mainTask);
        var configText = await configTask;
        var main = await mainTask ?? "";
        var provider = ProviderNamespace(main);
        var missing = new List<string>();
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            return (provider, types, missing);
        }
        var pattern = SwaggerOutputPattern(configText!, project, source.ApiVersion);
        if (pattern is null)
        {
            missing.Add("checked-in Swagger output mapping");
            return (provider, types, missing);
        }
        var parts = pattern.Split('/');
        var files = new List<string>();
        var visited = 0;
        async Task DiscoverAsync(int index, string directory)
        {
            if (++visited > 64 || files.Count > 32)
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
            var listing = await ReadGitHubAsync($"repos/{source.SpecRepository}/contents/{encoded}?ref={source.CommitSha}", token, ct);
            if (listing is null)
            {
                return;
            }
            if (listing is not JsonArray entries || entries.Count >= 1000)
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
                    if (files.Count > 32)
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
        await DiscoverAsync(0, "");
        foreach (var file in files)
        {
            var text = await ReadFileAsync(source.SpecRepository, source.CommitSha, file, token, ct);
            if (text is null)
            {
                continue;
            }
            JsonObject? document;
            try
            {
                document = JsonNode.Parse(text) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
                document = null;
            }
            if (document is null)
            {
                missing.Add($"Invalid checked-in Swagger: {file}");
                continue;
            }
            if (Text(document["info"]?["version"]) != source.ApiVersion)
            {
                missing.Add($"Swagger API version does not match SDK generation metadata: {file}");
                continue;
            }
            foreach (var paths in new[] { document["paths"], document["x-ms-paths"] }.OfType<JsonObject>())
            {
                foreach (var route in paths)
                {
                    var collection = route.Value is JsonObject operations && operations.ContainsKey("get");
                    types.UnionWith(ResourceTypesFromPath(route.Key, provider, collection));
                }
            }
        }
        if (types.Count == 0)
        {
            missing.Add("resource-type mapping");
        }
        return (provider, types, missing);
    }

    private static string? SwaggerOutputPattern(string text, string project, string apiVersion)
    {
        try
        {
            var config = ParseYaml(text);
            if (config.Children.ContainsKey(new YamlScalarNode("extends")) ||
                !config.Children.TryGetValue(new YamlScalarNode("options"), out var optionsNode) || optionsNode is not YamlMappingNode options ||
                !options.Children.TryGetValue(new YamlScalarNode("@azure-tools/typespec-autorest"), out var emitterNode) || emitterNode is not YamlMappingNode emitter)
            {
                return null;
            }
            var outputDirectory = config.Children.ContainsKey(new YamlScalarNode("output-dir")) ? Scalar(config, "output-dir") : "{project-root}/tsp-output";
            var emitterDirectory = Scalar(emitter, "emitter-output-dir");
            var outputFile = Scalar(emitter, "output-file");
            if (outputDirectory.Length == 0 || emitterDirectory.Length == 0 || outputFile.Length == 0)
            {
                return null;
            }
            outputDirectory = outputDirectory.Replace("{project-root}", project, StringComparison.Ordinal);
            emitterDirectory = emitterDirectory.Replace("{project-root}", project, StringComparison.Ordinal).Replace("{output-dir}", outputDirectory, StringComparison.Ordinal);
            outputFile = outputFile.Replace("{project-root}", project, StringComparison.Ordinal).Replace("{emitter-output-dir}", emitterDirectory, StringComparison.Ordinal)
                .Replace("{output-dir}", outputDirectory, StringComparison.Ordinal).Replace("{version-status}", apiVersion.Contains("preview", StringComparison.OrdinalIgnoreCase) ? "preview" : "stable", StringComparison.Ordinal)
                .Replace("{version}", apiVersion, StringComparison.Ordinal);
            outputFile = Regex.Replace(outputFile, @"\{[^}]+\}", "*");
            var candidate = outputFile.StartsWith("specification/", StringComparison.Ordinal) ? outputFile : $"{emitterDirectory}/{outputFile}";
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
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or FormatException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ResourceTypesFromPath(string path, string providerNamespace, bool collection)
    {
        var segments = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!segments[index].Equals("providers", StringComparison.OrdinalIgnoreCase) || !segments[index + 1].Equals(providerNamespace, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var types = new List<string>();
            for (var cursor = index + 2; cursor < segments.Length; cursor += 2)
            {
                var type = segments[cursor];
                if (!Regex.IsMatch(type, @"\A[\w.-]+\z") || type.Equals("operations", StringComparison.OrdinalIgnoreCase) || type.Equals("providers", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                var name = cursor + 1 < segments.Length ? segments[cursor + 1] : null;
                if (name is null && !collection)
                {
                    break;
                }
                if (name is not null && !Regex.IsMatch(name, @"\A\{[^}]+\}\z") && name != "default")
                {
                    types.Clear();
                    break;
                }
                types.Add(type);
            }
            if (types.Count > 0)
            {
                result.Add(string.Join('/', types));
            }
        }
        return result;
    }

    private static string SafeError(Exception exception) => exception is OperationCanceledException ? "The readiness check was canceled or exceeded its time budget." :
        exception is HttpRequestException httpException && httpException.StatusCode.HasValue ? $"A platform or application failure prevented the check (HTTP {(int)httpException.StatusCode.Value})." :
        "A platform or application failure prevented the check. No deployment conclusion can be drawn.";

    private static string ProviderNamespace(string main)
    {
        main = Regex.Replace(Regex.Replace(main, @"/\*[\s\S]*?\*/", ""), @"//[^\n]*", "");
        var explicitNamespace = Regex.Match(main, "@armProviderNamespace\\(\\s*\"([\\w.-]+)\"\\s*\\)").Groups[1].Value;
        return explicitNamespace.Length > 0 ? explicitNamespace :
            Regex.Match(main, @"@armProviderNamespace\b(?!\s*\()[\s\S]*?\bnamespace\s+([\w.]+)\s*[;{]").Groups[1].Value;
    }

    private async Task<string?> ReadFileAsync(string repository, string commit, string path, string token, CancellationToken ct)
    {
        var encoded = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        var file = await ReadGitHubAsync($"repos/{repository}/contents/{encoded}?ref={Uri.EscapeDataString(commit)}", token, ct);
        return file is null ? null : file["type"]?.GetValue<string>() == "file" && Text(file["encoding"]) == "base64" ?
            Encoding.UTF8.GetString(Convert.FromBase64String(Text(file["content"]))) :
            throw new InvalidOperationException("Expected a checked-in metadata file.");
    }

    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
    private static bool IsSha(string value) => Regex.IsMatch(value, @"\A[a-f0-9]{40}\z", RegexOptions.IgnoreCase);
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

    private static YamlMappingNode ParseYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode map ? map :
            throw new FormatException("Expected one YAML mapping.");
    }

    private static string Scalar(YamlMappingNode mapping, string name)
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
}
