using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IServiceApiReadinessService
{
    Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct);
}

public class ServiceApiReadinessService(IGitHubService github, IAzureService azure, IHttpClientFactory clients) : IServiceApiReadinessService
{
    public const string HttpClientName = "ServiceApiReadiness";
    // This selects the ARM metadata endpoint, not the SDK's service API version.
    public const string ArmMetadataApiVersion = "2021-04-01";
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    public async Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct)
    {
        var result = new ServiceApiReadinessResponse();
        // Authentication and all evidence reads share one deadline.
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
            // Capture one revision; publishing callers must check that it is still current.
            result.HeadSha = Text(pr["head"]?["sha"]);
            result.HeadRepository = Text(pr["head"]?["repo"]?["full_name"]);
            if (Text(pr["state"]) != "open" || !GeneratedSdkMetadataHelper.IsCommitSha(result.HeadSha) ||
                string.IsNullOrEmpty(result.HeadRepository))
            {
                result.Detail = "An open SDK PR with a current head repository and full head SHA is required.";
                return result;
            }
            var language = SdkLanguageHelpers.GetLanguageForRepo(repository);
            var metadata = new SdkReadinessMetadataHelper(_http, token);
            var snapshot = new SdkReadinessMetadataHelper.SdkSnapshot(result.HeadRepository, result.HeadSha, language,
                Text(pr["base"]?["repo"]?["full_name"]), Text(pr["base"]?["sha"]));
            var evidence = await metadata.ReadGenerationEvidenceAsync(prPath, snapshot, ct);
            result.Plane = evidence.Plane;
            if (evidence.Plane == SdkReadinessMetadataHelper.DataPlane)
            {
                result.Detail = "Data-plane SDK; ARM provider metadata is not applicable.";
                return result;
            }
            if (evidence.Issues.Count > 0 || evidence.Sources.Count == 0 || evidence.Plane == SdkReadinessMetadataHelper.UnknownPlane)
            {
                result.Detail = evidence.Issues.Count > 0 ? string.Join(" ", evidence.Issues) : "Authoritative SDK generation metadata or API plane is unavailable.";
                return result;
            }
            await ValidateProjectsAsync(evidence.Sources, result, token, ct);
            SetOverallResult(result);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Error;
            result.Detail = exception is ArgumentException ? "Provide an SDK repository and positive PR number." : SafeError(exception);
            result.ResponseError = string.IsNullOrEmpty(result.HeadSha) ? result.Detail :
                $"Checked SDK head: {result.HeadRepository}@{result.HeadSha}{Environment.NewLine}{result.Detail}";
        }
        return result;
    }

    private async Task ValidateProjectsAsync(IEnumerable<ServiceApiReadinessSource> sources, ServiceApiReadinessResponse result, string token, CancellationToken ct)
    {
        // Cache failed lookup tasks too, so projects do not retry the same ARM failure.
        var providers = new Dictionary<string, Task<JsonNode?>>(StringComparer.OrdinalIgnoreCase);
        var specifications = new TypeSpecReadinessHelper(_http, token);
        TokenCredential? credential = null;
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            var project = new ServiceApiReadinessProject { Source = source };
            result.Projects.Add(project);
            try
            {
                var spec = await specifications.ReadPinnedSwaggerResourceTypesAsync(source, ct);
                project.Namespace = spec.Namespace;
                if (spec.MissingEvidence.Count > 0)
                {
                    project.Detail = $"Missing or unresolved: {string.Join(", ", spec.MissingEvidence)}.";
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
                    providerTask = ReadTenantProviderAsync(spec.Namespace, credential, ct);
                    providers.Add(spec.Namespace, providerTask);
                }
                var provider = await providerTask;
                if (provider is null)
                {
                    project.Detail = "The provider is unavailable in the validation tenant.";
                    continue;
                }
                CompareApiVersion(project, provider, spec.ResourceTypes);
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
        // One exact match passes this project, but does not prove every operation or region.
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
            var failures = result.Projects.Where(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error)
                .Select(project => $"{project.Source.PackagePath}: {project.Detail}");
            result.ResponseError = $"Checked SDK head: {result.HeadRepository}@{result.HeadSha}{Environment.NewLine}" +
                string.Join(Environment.NewLine, failures);
        }
    }

    private Task<JsonNode?> ReadGitHubAsync(string path, string token, CancellationToken ct) =>
        GitHubService.ReadJsonEvidenceAsync(_http, path, token, ct);

    private async Task<JsonNode?> ReadTenantProviderAsync(string providerNamespace, TokenCredential credential, CancellationToken ct)
    {
        var accessToken = await credential.GetTokenAsync(new TokenRequestContext(["https://management.azure.com/.default"]), ct);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://management.azure.com/providers/{Uri.EscapeDataString(providerNamespace)}?api-version={ArmMetadataApiVersion}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        var text = await HttpResponseHelper.ReadBoundedResponseAsync(_http, request, ct);
        return text is null ? null : JsonNode.Parse(text);
    }

    private static string SafeError(Exception exception) => exception is OperationCanceledException ? "The readiness check was canceled or exceeded its time budget." :
        exception is HttpRequestException httpException && httpException.StatusCode.HasValue ? $"A platform or application failure prevented the check (HTTP {(int)httpException.StatusCode.Value})." :
        "A platform or application failure prevented the check. No deployment conclusion can be drawn.";

    private static string Text(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
}
