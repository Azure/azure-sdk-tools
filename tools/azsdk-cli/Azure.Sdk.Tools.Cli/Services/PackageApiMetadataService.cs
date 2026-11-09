using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IPackageApiMetadataService
{
    Task<PackageInfo> GetPackageInfoAsync(PackageInfo package, CancellationToken ct = default);
}

public sealed class PackageApiMetadataService(IGitHubService github, IHttpClientFactory clients) : IPackageApiMetadataService
{
    public const string HttpClientName = "PackageApiMetadata";
    internal static readonly TimeSpan EvidenceTimeout = TimeSpan.FromSeconds(40);
    private readonly HttpClient _http = clients.CreateClient(HttpClientName);

    internal sealed record PackageEvidence(PackageInfo Package, List<PackageApiSource> Sources, List<string> Issues);

    public async Task<PackageInfo> GetPackageInfoAsync(PackageInfo package, CancellationToken ct = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(EvidenceTimeout);
        ct = budget.Token;
        var metadata = await ReadLocalGenerationEvidenceAsync(package, ct);
        if (metadata.Plane == SdkPackageMetadataHelper.DataPlane)
        {
            package.ResourceTypes = [];
            return package;
        }
        if (metadata.Issues.Count > 0 || metadata.Sources.Count == 0)
        {
            throw new InvalidOperationException(string.Join(" ", metadata.Issues.DefaultIfEmpty("Package generation metadata is unavailable.")));
        }
        var evidence = await PopulateResourceTypesAsync(package, metadata.Sources, ct);
        if (evidence.Issues.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", evidence.Issues));
        }
        return evidence.Package;
    }

    internal Task<SdkPackageMetadataHelper.GenerationEvidence> ReadLocalGenerationEvidenceAsync(PackageInfo package, CancellationToken ct)
    {
        return new SdkPackageMetadataHelper(_http, github.GetAuthToken(ct)).ReadLocalGenerationEvidenceAsync(package, ct);
    }

    internal async Task<PackageEvidence> PopulateResourceTypesAsync(PackageInfo package, IEnumerable<PackageApiSource> generationSources, CancellationToken ct)
    {
        var sources = generationSources.ToArray();
        package.ApiVersions = [.. sources.Select(source => new PackageApiVersion(source.ApiVersion, source.NamespaceIsInferred ? null : source.ExpectedNamespace))
            .Distinct().OrderBy(version => version.Namespace, StringComparer.Ordinal).ThenBy(version => version.Version, StringComparer.Ordinal)];
        package.ResourceTypes = [];
        var resolvedSources = new List<PackageApiSource>();
        var issues = new List<string>();
        var resources = new HashSet<PackageResourceType>();
        var resourceTypeHelper = new ProjectResourceTypeHelper(_http, github.GetAuthToken(ct));
        foreach (var group in sources.GroupBy(source => (source.SpecRepository, source.CommitSha, source.Configuration, source.ApiVersion)))
        {
            var sourceProject = group.Key;
            var resourceEvidence = await resourceTypeHelper.ReadPinnedSwaggerResourceTypesAsync(sourceProject.SpecRepository, sourceProject.CommitSha,
                sourceProject.Configuration, sourceProject.ApiVersion, ct);
            if (resourceEvidence.MissingEvidence.Count > 0)
            {
                issues.AddRange(resourceEvidence.MissingEvidence);
                resolvedSources.AddRange(group);
                continue;
            }
            foreach (var source in group)
            {
                var namespaces = source.NamespaceIsInferred
                    ? resourceEvidence.ProviderNamespaces
                    : [source.ExpectedNamespace];
                foreach (var providerNamespace in namespaces)
                {
                    var matching = resourceEvidence.ResourceTypes.Where(type => type.Namespace.Equals(providerNamespace, StringComparison.OrdinalIgnoreCase) && type.ApiVersion == source.ApiVersion).ToArray();
                    if (matching.Length == 0)
                    {
                        issues.Add($"SDK metadata namespace {providerNamespace} has no resource-type mapping in the pinned project.");
                    }
                    resources.UnionWith(matching);
                    resolvedSources.Add(new PackageApiSource
                    {
                        PackagePath = source.PackagePath,
                        Configuration = source.Configuration,
                        CommitSha = source.CommitSha,
                        SpecRepository = source.SpecRepository,
                        ApiVersion = source.ApiVersion,
                        ExpectedNamespace = providerNamespace,
                        NamespaceIsInferred = source.NamespaceIsInferred,
                        MetadataFiles = [.. source.MetadataFiles]
                    });
                }
            }
        }
        package.ResourceTypes = [.. resources.OrderBy(type => type.Namespace, StringComparer.Ordinal)
            .ThenBy(type => type.ApiVersion, StringComparer.Ordinal).ThenBy(type => type.ResourceType, StringComparer.Ordinal)];
        var distinctSources = resolvedSources.GroupBy(source => source.ExpectedNamespace, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.DistinctBy(source => source.ApiVersion)).ToList();
        return new(package, distinctSources, [.. issues.Distinct(StringComparer.Ordinal)]);
    }
}