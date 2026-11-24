using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using static Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelper;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IServiceApiReadinessService
{
    Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct);
    Task<ServiceApiReadinessResponse> GetPackageMetadataAsync(string owner, string repository, int pullRequest, CancellationToken ct);
    Task<ServiceApiReadinessResponse> ValidateAsync(ServiceApiReadinessResponse metadata, CancellationToken ct);
    Task<ServiceApiReadinessResponse> CheckPackageAsync(PackageInfo package, CancellationToken ct);
}

public class ServiceApiReadinessService(IGitHubService github, IAzureService azure, IHttpClientFactory clients,
    IPackageApiMetadataService packageMetadataService) : IServiceApiReadinessService
{
    public const string HttpClientName = PackageApiMetadataService.HttpClientName;
    // This selects the ARM metadata endpoint, not the SDK's service API version.
    public const string ArmMetadataApiVersion = "2021-04-01";
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    public Task<ServiceApiReadinessResponse> CheckAsync(string owner, string repository, int pullRequest, CancellationToken ct) =>
        CheckPullRequestAsync(owner, repository, pullRequest, false, ct);

    public Task<ServiceApiReadinessResponse> GetPackageMetadataAsync(string owner, string repository, int pullRequest, CancellationToken ct) =>
        CheckPullRequestAsync(owner, repository, pullRequest, true, ct);

    private async Task<ServiceApiReadinessResponse> CheckPullRequestAsync(string owner, string repository, int pullRequest, bool metadataOnly, CancellationToken ct)
    {
        var result = new ServiceApiReadinessResponse();
        // Authentication and all evidence reads share one deadline.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PackageApiMetadataService.EvidenceTimeout);
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
            var pr = await GitHubService.ReadJsonEvidenceAsync(_http, prPath, token, ct) ?? throw new InvalidOperationException("SDK PR is unavailable.");
            // Capture one revision; publishing callers must check that it is still current.
            result.HeadSha = ReadJsonString(pr["head"]?["sha"]);
            result.HeadRepository = ReadJsonString(pr["head"]?["repo"]?["full_name"]);
            if (ReadJsonString(pr["state"]) != "open" || !GeneratedSdkMetadataHelper.IsCommitSha(result.HeadSha) ||
                string.IsNullOrEmpty(result.HeadRepository))
            {
                result.Detail = "An open SDK PR with a current head repository and full head SHA is required.";
                return result;
            }
            var language = SdkLanguageHelpers.GetLanguageForRepo(repository);
            var sdkPackageMetadataHelper = new SdkPackageMetadataHelper(_http, token);
            var snapshot = new SdkPackageMetadataHelper.SdkSnapshot(result.HeadRepository, result.HeadSha, language,
                ReadJsonString(pr["base"]?["repo"]?["full_name"]), ReadJsonString(pr["base"]?["sha"]));
            var discovery = await sdkPackageMetadataHelper.DiscoverPackagesAsync(prPath, snapshot, ct);
            result.Packages.AddRange(discovery.Packages);
            foreach (var package in result.Packages)
            {
                if (package.MetadataIssues.Length == 0)
                {
                    await sdkPackageMetadataHelper.PopulateGenerationMetadataAsync(package, snapshot, ct);
                }
            }
            if (!HasValidationEvidence(result.Packages, discovery.Issues, result))
            {
                return result;
            }
            await PopulateValidationProjectsAsync(result, ct);
            if (metadataOnly)
            {
                result.Detail = "Pinned package metadata and provider/API-version requirements only; no ARM readiness check was performed.";
                return result;
            }
            await ValidatePackagesAsync(result.Packages, result, ct);
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

    public async Task<ServiceApiReadinessResponse> CheckPackageAsync(PackageInfo package, CancellationToken ct)
    {
        var result = new ServiceApiReadinessResponse { InputKind = "package", Packages = [package] };
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(PackageApiMetadataService.EvidenceTimeout);
        ct = budget.Token;
        try
        {
            package = await packageMetadataService.ReadLocalGenerationEvidenceAsync(package, ct);
            result.Packages = [package];
            if (!HasValidationEvidence(result.Packages, [], result))
            {
                return result;
            }
            await PopulateValidationProjectsAsync(result, ct);
            await ValidatePackagesAsync(result.Packages, result, ct);
            SetOverallResult(result);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result.ReadinessStatus = ServiceApiReadinessResponse.Error;
            result.Detail = exception is ArgumentException or DirectoryNotFoundException
                ? "Provide an existing local SDK package directory." : SafeError(exception);
            result.ResponseError = result.Detail;
        }
        return result;
    }

    public async Task<ServiceApiReadinessResponse> ValidateAsync(ServiceApiReadinessResponse metadata, CancellationToken ct)
    {
        if (metadata.ReadinessStatus == ServiceApiReadinessResponse.Error ||
            metadata.Plane != SdkPackageMetadataHelper.ManagementPlane || metadata.Projects.Count == 0)
        {
            return metadata;
        }
        try
        {
            await ValidatePackagesAsync(metadata.Packages, metadata, ct);
            SetOverallResult(metadata);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            metadata.ReadinessStatus = ServiceApiReadinessResponse.Error;
            metadata.Detail = SafeError(exception);
            metadata.ResponseError = metadata.Detail;
        }
        return metadata;
    }

    private static bool HasValidationEvidence(IReadOnlyList<PackageInfo> packages, IReadOnlyList<string> discoveryIssues, ServiceApiReadinessResponse result)
    {
        var issues = discoveryIssues.Concat(packages.SelectMany(package => package.MetadataIssues)).Distinct(StringComparer.Ordinal).ToList();
        var planes = packages.Select(package => package.SdkType).ToHashSet();
        if (planes.Contains(SdkType.Management) && planes.Contains(SdkType.Dataplane))
        {
            issues.Add("Mixed SDK API planes require separate checks.");
        }
        result.Plane = discoveryIssues.Count == 0 && planes.Count == 1 ? planes.Single() switch
        {
            SdkType.Management => SdkPackageMetadataHelper.ManagementPlane,
            SdkType.Dataplane => SdkPackageMetadataHelper.DataPlane,
            _ => SdkPackageMetadataHelper.UnknownPlane
        } : SdkPackageMetadataHelper.UnknownPlane;
        if (result.Plane == SdkPackageMetadataHelper.DataPlane && issues.Count == 0)
        {
            result.Detail = "Data-plane SDK; ARM provider metadata is not applicable.";
            return false;
        }
        if (issues.Count > 0 || packages.Count == 0 || packages.All(package => package.ApiVersions.Length == 0) || result.Plane == SdkPackageMetadataHelper.UnknownPlane)
        {
            result.Detail = issues.Count > 0 ? string.Join(" ", issues) : packages.Count == 0
                ? "No currently changed SDK packages remain at the checked head." : "Authoritative SDK generation metadata or API plane is unavailable.";
            return false;
        }
        return true;
    }

    private async Task PopulateValidationProjectsAsync(ServiceApiReadinessResponse result, CancellationToken ct)
    {
        for (var index = 0; index < result.Packages.Count; index++)
        {
            var package = result.Packages[index];
            ct.ThrowIfCancellationRequested();
            var versions = package.ResolvedApiVersions;
            if (versions.Length == 0 || package.ApiVersions.Any(version => version.Namespace is null))
            {
                try
                {
                    package = await packageMetadataService.PopulateResourceTypesAsync(package, ct);
                    result.Packages[index] = package;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    result.Projects.AddRange(versions.Select(version => new ServiceApiReadinessProject
                    {
                        Package = package,
                        ApiVersion = version,
                        Namespace = version.Namespace ?? "",
                        ReadinessStatus = ServiceApiReadinessResponse.Error,
                        Detail = SafeError(exception)
                    }));
                    continue;
                }
            }
            result.Projects.AddRange(package.ResolvedApiVersions.Select(version => new ServiceApiReadinessProject
            {
                Package = package,
                ApiVersion = version,
                Namespace = version.Namespace ?? "",
                Detail = package.MetadataIssues.Length > 0
                    ? $"Missing or unresolved: {string.Join(", ", package.MetadataIssues)}." : ""
            }));
        }
    }

    private async Task ValidatePackagesAsync(IReadOnlyList<PackageInfo> packages, ServiceApiReadinessResponse result, CancellationToken ct)
    {
        // Cache failed lookup tasks too, so projects do not retry the same ARM failure.
        var providers = new Dictionary<string, Task<JsonNode?>>(StringComparer.OrdinalIgnoreCase);
        TokenCredential? credential = null;
        foreach (var package in packages)
        {
            if (package.MetadataIssues.Length > 0)
            {
                continue;
            }
            foreach (var project in result.Projects.Where(project => ReferenceEquals(project.Package, package) &&
                project.ReadinessStatus != ServiceApiReadinessResponse.Error))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!providers.TryGetValue(project.Namespace, out var providerTask))
                    {
                        credential ??= azure.GetCredential();
                        providerTask = ReadTenantProviderAsync(project.Namespace, credential, ct);
                        providers.Add(project.Namespace, providerTask);
                    }
                    var provider = await providerTask;
                    if (provider is null)
                    {
                        project.Detail = "The provider is unavailable in the validation tenant.";
                        continue;
                    }
                    CompareApiVersion(project, provider);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    project.ReadinessStatus = ServiceApiReadinessResponse.Error;
                    project.Detail = SafeError(exception);
                }
            }
        }
    }

    private static void CompareApiVersion(ServiceApiReadinessProject project, JsonNode provider)
    {
        if (!ReadJsonString(provider["namespace"]).Equals(project.Namespace, StringComparison.OrdinalIgnoreCase) ||
            provider["resourceTypes"] is not JsonArray advertisedTypes)
        {
            throw new InvalidOperationException("Unexpected ARM provider metadata response.");
        }
        var applicable = advertisedTypes.Where(resource => !string.IsNullOrWhiteSpace(ReadJsonString(resource?["resourceType"]))).ToArray();
        if (applicable.Length == 0)
        {
            project.Detail = "The provider metadata contains no resource types.";
            return;
        }
        foreach (var resource in applicable)
        {
            if (resource?["apiVersions"] is not JsonArray advertised || !advertised.Select(ReadJsonString).Contains(project.ApiVersion.Version, StringComparer.Ordinal))
            {
                continue;
            }
            var regions = (resource["locations"] as JsonArray ?? []).Select(ReadJsonString)
                .Concat((resource["locationMappings"] as JsonArray ?? []).Select(mapping => ReadJsonString(mapping?["location"])))
                .Where(region => !string.IsNullOrEmpty(region)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            project.Matches.Add(new ServiceApiReadinessMatch { ResourceType = ReadJsonString(resource["resourceType"]), Regions = regions });
        }
        // One exact match passes this project, but does not prove every operation or region.
        project.ReadinessStatus = project.Matches.Count > 0 ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected;
        project.Detail = $"{project.Matches.Count} of {applicable.Length} provider resource types advertise the exact version.";
        if (project.ReadinessStatus == ServiceApiReadinessResponse.NotDetected)
        {
            project.Detail += " Ask the service owner to confirm deployment; this is not a confirmed deployment failure.";
        }
    }

    internal static string GetOverallStatus(IEnumerable<string> projectStatuses)
    {
        var statuses = projectStatuses.ToArray();
        if (statuses.Contains(ServiceApiReadinessResponse.Error))
        {
            return ServiceApiReadinessResponse.Error;
        }
        if (statuses.Contains(ServiceApiReadinessResponse.Inconclusive))
        {
            return ServiceApiReadinessResponse.Inconclusive;
        }
        return statuses.All(status => status == ServiceApiReadinessResponse.Passed)
            ? ServiceApiReadinessResponse.Passed : ServiceApiReadinessResponse.NotDetected;
    }

    internal static void SetOverallResult(ServiceApiReadinessResponse result)
    {
        result.ReadinessStatus = GetOverallStatus(result.Projects.Select(project => project.ReadinessStatus));
        result.Detail = result.InputKind == "package"
            ? "API versions were read from local SDK metadata. Working-tree content was inspected; this is not a PR-head attestation. Provider metadata is evidence, not release approval or proof of regional deployment."
            : "API versions were read from SDK metadata files at the checked PR head. Provider metadata is evidence, not release approval or proof of regional deployment.";
        if (result.ReadinessStatus == ServiceApiReadinessResponse.Error)
        {
            var failures = result.Projects.Where(project => project.ReadinessStatus == ServiceApiReadinessResponse.Error)
                .Select(project => $"{project.Package.DirectoryPath}: {project.Detail}");
            var context = result.InputKind == "package" ? "Local SDK package check:" : $"Checked SDK head: {result.HeadRepository}@{result.HeadSha}";
            result.ResponseError = $"{context}{Environment.NewLine}" +
                string.Join(Environment.NewLine, failures);
        }
    }

    private async Task<JsonNode?> ReadTenantProviderAsync(string providerNamespace, TokenCredential credential, CancellationToken ct)
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

    private static string SafeError(Exception exception) => exception is OperationCanceledException ? "The readiness check was canceled or exceeded its time budget." :
        exception is HttpRequestException httpException && httpException.StatusCode.HasValue ? $"A platform or application failure prevented the check (HTTP {(int)httpException.StatusCode.Value})." :
        "A platform or application failure prevented the check. No deployment conclusion can be drawn.";

}
