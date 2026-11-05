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
    Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct);
}

public class ServiceApiReadinessService(IGitHubService github, IAzureService azure, IHttpClientFactory clients) : IServiceApiReadinessService
{
    public const string HttpClientName = "ServiceApiReadiness";
    public const string ArmMetadataApiVersion = "2021-04-01";
    private const string ResourceManager = "resource-manager";
    private const string ManagementPlane = "management-plane";
    private const string DataPlane = "data-plane";
    private const string UnknownPlane = "unknown";
    private const int MaxEvidenceBytes = 4 * 1024 * 1024;
    private const int MaxMetadataFiles = 32;
    private const int MaxSwaggerFiles = 32;
    private const int MaxSwaggerDirectories = 64;
    private const int MaxDirectoryEntries = 1000;
    private const string GenerationLocationFile = "tsp-location.yaml";
    private readonly record struct SdkSnapshot(string Repository, string HeadSha, SdkLanguage Language);
    private sealed record GenerationLocation(string Directory, string CommitSha, string Repository);
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    public async Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct)
    {
        var result = new ServiceApiReadinessResponse();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(40));
        ct = budget.Token;
        try
        {
            if (!Regex.IsMatch(owner, @"\A[\w.-]+\z") ||
                !Regex.IsMatch(repository, @"\Aazure-sdk-for-(net|java|js|python|go)\z", RegexOptions.IgnoreCase) ||
                pullRequest <= 0)
            {
                throw new ArgumentException("Provide an SDK repository and positive PR number.");
            }
            ct.ThrowIfCancellationRequested();
            var token = github.GetAuthToken(ct);
            var prPath = $"repos/{owner}/{repository}/pulls/{pullRequest}";
            var pr = await ReadGitHubAsync(prPath, token, ct) ?? throw new InvalidOperationException("SDK PR is unavailable.");
            result.HeadSha = Text(pr["head"]?["sha"]);
            result.HeadRepository = Text(pr["head"]?["repo"]?["full_name"]);
            if (Text(pr["state"]) != "open" || !IsSha(result.HeadSha) ||
                string.IsNullOrEmpty(result.HeadRepository))
            {
                result.Detail = "An open SDK PR with a current head repository and full head SHA is required.";
                return result;
            }
            var language = SdkLanguageHelpers.GetLanguageForRepo(repository);
            var changedPackages = await GetChangedPackagesAsync(prPath, language, token, ct);
            if (changedPackages is null)
            {
                result.Detail = "The changed-file listing reached GitHub's 3000-file limit; package discovery is incomplete.";
                return result;
            }
            var (sources, plane, issues) = await ReadGenerationSourcesAsync(
                changedPackages.Value, result.HeadRepository, result.HeadSha, language, token, ct);
            result.Plane = plane;
            if (plane == DataPlane)
            {
                result.Detail = "Data-plane SDK; ARM provider metadata is not applicable.";
                return result;
            }
            if (issues.Count > 0 || sources.Count == 0 || plane == UnknownPlane)
            {
                result.Detail = issues.Count > 0 ? string.Join(" ", issues) : "Authoritative SDK generation metadata or API plane is unavailable.";
                return result;
            }
            await EvaluateProjectsAsync(sources, result, token, ct);
            SetOverallResult(result);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Error;
            result.Detail = exception is ArgumentException ? "Provide an SDK repository and positive PR number." : SafeError(exception);
            result.ResponseError = result.Detail;
        }
        return result;
    }

    private async Task<(List<ServiceApiReadinessSource> Sources, string Plane, List<string> Issues)> ReadGenerationSourcesAsync(
        (HashSet<string> Packages, HashSet<string> KnownAtHead) changedPackages,
        string headRepository, string headSha, SdkLanguage language, string token, CancellationToken ct)
    {
        var (packages, knownAtHead) = changedPackages;
        var sdk = new SdkSnapshot(headRepository, headSha, language);
        var issues = new List<string>();
        var sources = new List<ServiceApiReadinessSource>();
        var planes = new HashSet<string>();
        var survivingPackages = 0;
        foreach (var package in packages.Order(StringComparer.Ordinal))
        {
            bool? exists = knownAtHead.Contains(package) ? true : await PackageExistsAtHeadAsync(package, sdk, token, ct);
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
            var (location, plane) = await ReadGenerationLocationAsync(package, sdk, token, issues, ct);
            planes.Add(plane);
            if (location is null || plane == DataPlane)
            {
                continue;
            }
            sources.AddRange(await ReadPackageSourcesAsync(package, location, sdk, token, issues, ct));
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

    private async Task<bool?> PackageExistsAtHeadAsync(string package, SdkSnapshot sdk, string token, CancellationToken ct)
    {
        var encoded = string.Join('/', package.Split('/').Select(Uri.EscapeDataString));
        var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", token, ct);
        return listing switch { null => false, JsonArray => true, _ => null };
    }

    private async Task<(GenerationLocation? Location, string Plane)> ReadGenerationLocationAsync(
        string package, SdkSnapshot sdk, string token, List<string> issues, CancellationToken ct)
    {
        var path = $"{package}/{GenerationLocationFile}";
        var text = await ReadFileAsync(sdk.Repository, sdk.HeadSha, path, token, ct);
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
            if (!IsSpecificationProjectDirectory(directory) || !IsSha(commit) || repository is null)
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
        if (segments.Contains(ResourceManager) || directory.Contains(".Management", StringComparison.OrdinalIgnoreCase))
        {
            return ManagementPlane;
        }
        return UnknownPlane;
    }

    private async Task<List<string>> ReadMetadataPathsAsync(string package, SdkSnapshot sdk, string token, List<string> issues, CancellationToken ct)
    {
        var location = GeneratedSdkMetadataHelper.GetMetadataFileLocation(sdk.Language);
        var directory = location.Directory.Length == 0 ? package : $"{package}/{location.Directory}";
        if (sdk.Language != SdkLanguage.Java)
        {
            return [$"{directory}/{location.Pattern}"];
        }
        var encoded = string.Join('/', directory.Split('/').Select(Uri.EscapeDataString));
        var listing = await ReadGitHubAsync($"repos/{sdk.Repository}/contents/{encoded}?ref={sdk.HeadSha}", token, ct);
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
        string package, GenerationLocation location, SdkSnapshot sdk, string token, List<string> issues, CancellationToken ct)
    {
        var metadataPaths = await ReadMetadataPathsAsync(package, sdk, token, issues, ct);
        var sources = new List<ServiceApiReadinessSource>();
        string? inferredProvider = null;
        foreach (var metadataPath in metadataPaths)
        {
            var text = await ReadFileAsync(sdk.Repository, sdk.HeadSha, metadataPath, token, ct);
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
                    inferredProvider ??= ProviderNamespace(await ReadFileAsync(location.Repository, location.CommitSha, $"{location.Directory}/main.tsp", token, ct) ?? "");
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

    private async Task EvaluateProjectsAsync(IEnumerable<ServiceApiReadinessSource> sources, ServiceApiReadinessResponse result, string token, CancellationToken ct)
    {
        var providers = new Dictionary<string, Task<JsonNode?>>(StringComparer.OrdinalIgnoreCase);
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
                if (!providers.TryGetValue(spec.Namespace, out var providerTask))
                {
                    credential ??= azure.GetCredential();
                    providerTask = ReadProviderAsync(spec.Namespace, credential, ct);
                    providers.Add(spec.Namespace, providerTask);
                }
                var provider = await providerTask;
                if (provider is null)
                {
                    project.Detail = "The provider is unavailable in the validation tenant.";
                    continue;
                }
                CompareApiVersion(project, provider, spec.Types);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                project.ReadinessStatus = ServiceApiReadinessResponse.Error;
                project.Detail = SafeError(exception);
            }
        }
    }

    private static void CompareApiVersion(ServiceApiReadinessProject project, JsonNode provider, IReadOnlySet<string> resourceTypes)
    {
        if (!Text(provider["namespace"]).Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) ||
            provider["resourceTypes"] is not JsonArray advertisedTypes)
        {
            throw new InvalidOperationException("Unexpected ARM provider metadata response.");
        }
        var applicable = advertisedTypes.Where(resource => resourceTypes.Contains(Text(resource?["resourceType"]))).ToArray();
        if (applicable.Length == 0)
        {
            project.Detail = "No project resource type could be mapped to provider metadata.";
            return;
        }
        foreach (var resource in applicable)
        {
            if (resource?["apiVersions"] is not JsonArray advertised || !advertised.Select(Text).Contains(project.Source.ApiVersion, StringComparer.Ordinal))
            {
                continue;
            }
            var regions = (resource["locations"] as JsonArray ?? []).Select(Text)
                .Concat((resource["locationMappings"] as JsonArray ?? []).Select(mapping => Text(mapping?["location"])))
                .Where(region => !string.IsNullOrEmpty(region)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            project.Matches.Add(new ServiceApiReadinessMatch { ResourceType = Text(resource["resourceType"]), Regions = regions });
        }
        project.ReadinessStatus = project.Matches.Count > 0 ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected;
        project.Detail = $"{project.Matches.Count} of {applicable.Length} mapped resource types advertise the exact version ({resourceTypes.Count} project resource types identified).";
        if (project.ReadinessStatus == ServiceApiReadinessResponse.NotDetected)
        {
            project.Detail += " Ask the service owner to confirm deployment; this is not a confirmed deployment failure.";
        }
    }

    private static void SetOverallResult(ServiceApiReadinessResponse result)
    {
        if (result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error))
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Error;
        }
        else if (result.Projects.Any(project => project.ReadinessStatus == ServiceApiReadinessResponse.Inconclusive))
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Inconclusive;
        }
        else if (result.Projects.All(project => project.ReadinessStatus == ServiceApiReadinessResponse.Passed))
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Passed;
        }
        else
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.NotDetected;
        }
        result.Detail = "API versions were read from SDK metadata files at the checked PR head. Provider metadata is evidence, not release approval or proof of regional deployment.";
        if (result.ReadinessStatus == ServiceApiReadinessResponse.Error)
        {
            result.ResponseError = "A platform or application failure prevented the readiness check.";
        }
    }

    private async Task<(HashSet<string> Packages, HashSet<string> KnownAtHead)?> GetChangedPackagesAsync(
        string prPath, SdkLanguage language, string token, CancellationToken ct)
    {
        const int pageSize = 100;
        const int maxPages = 30;
        var packages = new HashSet<string>(StringComparer.Ordinal);
        var knownAtHead = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 1; page <= maxPages; page++)
        {
            var files = await ReadGitHubAsync($"{prPath}/files?per_page={pageSize}&page={page}", token, ct) as JsonArray
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

    private async Task<JsonNode?> ReadGitHubAsync(string path, string token, CancellationToken ct)
    {
        var text = await ReadGitHubTextAsync(path, token, "application/vnd.github+json", ct);
        return text is null ? null : JsonNode.Parse(text);
    }

    private async Task<string?> ReadGitHubTextAsync(string path, string token, string mediaType, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        request.Headers.UserAgent.ParseAdd("azsdk-service-api-readiness");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return await ReadEvidenceAsync(request, ct);
    }

    private async Task<string?> ReadEvidenceAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxEvidenceBytes)
        {
            throw new InvalidOperationException("Response exceeded the evidence size limit.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var content = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + count > MaxEvidenceBytes)
            {
                throw new InvalidOperationException("Response exceeded the evidence size limit.");
            }
            await content.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return Encoding.UTF8.GetString(content.ToArray());
    }

    private async Task<JsonNode?> ReadProviderAsync(string providerNamespace, TokenCredential credential, CancellationToken ct)
    {
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com/providers/{Uri.EscapeDataString(providerNamespace)}?api-version={ArmMetadataApiVersion}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        var text = await ReadEvidenceAsync(request, ct);
        return text is null ? null : JsonNode.Parse(text);
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
        var files = await DiscoverSwaggerFilesAsync(pattern, source, token, ct);
        foreach (var file in files)
        {
            var text = await ReadFileAsync(source.SpecRepository, source.CommitSha, file, token, ct);
            if (text is not null)
            {
                AddSwaggerResourceTypes(text, file, source.ApiVersion, provider, types, missing);
            }
        }
        if (types.Count == 0)
        {
            missing.Add("resource-type mapping");
        }
        return (provider, types, missing);
    }

    private async Task<List<string>> DiscoverSwaggerFilesAsync(string pattern, ServiceApiReadinessSource source, string token, CancellationToken ct)
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
            var listing = await ReadGitHubAsync($"repos/{source.SpecRepository}/contents/{encoded}?ref={source.CommitSha}", token, ct);
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

    private static void AddSwaggerResourceTypes(string text, string file, string apiVersion, string provider,
        HashSet<string> types, List<string> missing)
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
        if (Text(document["info"]?["version"]) != apiVersion)
        {
            missing.Add($"Swagger API version does not match SDK generation metadata: {file}");
            return;
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
            var providerFolder = Scalar(emitter, "azure-resource-provider-folder");
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
                    types.Clear();
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
        var requestPath = $"repos/{repository}/contents/{encoded}?ref={Uri.EscapeDataString(commit)}";
        var file = await ReadGitHubAsync(requestPath, token, ct);
        if (file is null)
        {
            return null;
        }
        if (Text(file["type"]) != "file")
        {
            throw new InvalidOperationException("Expected a checked-in metadata file.");
        }
        if (file["size"] is JsonValue size && size.TryGetValue<long>(out var bytes) && bytes > MaxEvidenceBytes)
        {
            throw new InvalidOperationException("File exceeded the evidence size limit.");
        }
        return Text(file["encoding"]) switch
        {
            "base64" => Encoding.UTF8.GetString(Convert.FromBase64String(Text(file["content"]))),
            "none" => await ReadGitHubTextAsync(requestPath, token, "application/vnd.github.raw+json", ct),
            _ => throw new InvalidOperationException("Expected checked-in file content.")
        };
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
