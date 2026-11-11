using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IPackageApiMetadataService
{
    Task<PackageInfo> GetPackageInfoAsync(PackageInfo package, CancellationToken ct = default);
    Task<PackageInfo> ReadLocalGenerationEvidenceAsync(PackageInfo package, CancellationToken ct = default);
    Task<PackageInfo> PopulateResourceTypesAsync(PackageInfo package, CancellationToken ct = default);
}

public sealed class PackageApiMetadataService(IGitHubService github, IHttpClientFactory clients) : IPackageApiMetadataService
{
    public const string HttpClientName = "PackageApiMetadata";
    internal static readonly TimeSpan EvidenceTimeout = TimeSpan.FromSeconds(40);
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    public async Task<PackageInfo> GetPackageInfoAsync(PackageInfo package, CancellationToken ct = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(EvidenceTimeout);
        ct = budget.Token;
        package = await ReadLocalGenerationEvidenceAsync(package, ct);
        if (package.SdkType == SdkType.Dataplane)
        {
            package.ResourceTypes = [];
            return package;
        }
        if (package.MetadataIssues.Length > 0 || package.ApiVersions.Length == 0)
        {
            throw new InvalidOperationException(string.Join(" ", package.MetadataIssues.DefaultIfEmpty("Package generation metadata is unavailable.")));
        }
        package = await PopulateResourceTypesAsync(package, ct);
        if (package.MetadataIssues.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", package.MetadataIssues));
        }
        return package;
    }

    public Task<PackageInfo> ReadLocalGenerationEvidenceAsync(PackageInfo package, CancellationToken ct = default)
    {
        return new SdkPackageMetadataHelper(_http, github.GetAuthToken(ct)).ReadLocalGenerationEvidenceAsync(package, ct);
    }

    public async Task<PackageInfo> PopulateResourceTypesAsync(PackageInfo package, CancellationToken ct = default)
    {
        var location = package.TypeSpecLocation ?? throw new InvalidOperationException("Package generation metadata is unavailable.");
        package.ResourceTypes = [];
        var resolvedVersions = new List<PackageApiVersion>();
        var issues = new List<string>();
        var resources = new HashSet<PackageResourceType>();
        var resourceTypeHelper = new ProjectResourceTypeHelper(_http, github.GetAuthToken(ct));
        foreach (var group in package.ApiVersions.GroupBy(version => version.Version, StringComparer.Ordinal))
        {
            var resourceEvidence = await resourceTypeHelper.ReadPinnedSwaggerResourceTypesAsync(location.Repository, location.Commit,
                $"{location.Directory}/tspconfig.yaml", group.Key, ct);
            if (resourceEvidence.MissingEvidence.Count > 0)
            {
                issues.AddRange(resourceEvidence.MissingEvidence);
                resolvedVersions.AddRange(package.ResolvedApiVersions.Where(version => version.Version == group.Key));
                continue;
            }
            foreach (var version in group)
            {
                var namespaces = version.Namespace is null
                    ? resourceEvidence.ProviderNamespaces
                    : [version.Namespace];
                foreach (var providerNamespace in namespaces)
                {
                    var matching = resourceEvidence.ResourceTypes.Where(type => type.Namespace.Equals(providerNamespace, StringComparison.OrdinalIgnoreCase) && type.ApiVersion == version.Version).ToArray();
                    if (matching.Length == 0)
                    {
                        issues.Add($"SDK metadata namespace {providerNamespace} has no resource-type mapping in the pinned project.");
                    }
                    resources.UnionWith(matching);
                    resolvedVersions.Add(new PackageApiVersion(version.Version, providerNamespace));
                }
            }
        }
        package.ResourceTypes = [.. resources.OrderBy(type => type.Namespace, StringComparer.Ordinal)
            .ThenBy(type => type.ApiVersion, StringComparer.Ordinal).ThenBy(type => type.ResourceType, StringComparer.Ordinal)];
        package.ResolvedApiVersions = [.. resolvedVersions.GroupBy(version => version.Namespace, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.DistinctBy(version => version.Version))];
        package.MetadataIssues = [.. issues.Distinct(StringComparer.Ordinal)];
        return package;
    }
}