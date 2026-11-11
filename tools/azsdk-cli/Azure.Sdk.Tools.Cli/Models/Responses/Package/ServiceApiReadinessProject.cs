using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceApiReadinessProject
{
    [JsonIgnore]
    public PackageInfo Package { get; set; } = new();

    [JsonIgnore]
    public PackageApiVersion ApiVersion { get; set; } = new("");

    [JsonPropertyName("source")]
    public JsonObject Source => new()
    {
        ["packagePath"] = Package.DirectoryPath.ToString(),
        ["configuration"] = Package.TypeSpecLocation is { } location ? $"{location.Directory}/tspconfig.yaml" : "",
        ["commitSha"] = Package.TypeSpecLocation?.Commit ?? "",
        ["specRepository"] = Package.TypeSpecLocation?.Repository ?? "",
        ["apiVersion"] = ApiVersion.Version,
        ["expectedNamespace"] = Namespace,
        ["metadataFiles"] = JsonSerializer.SerializeToNode(Package.MetadataFiles)
    };

    [JsonPropertyName("status")]
    public string ReadinessStatus { get; set; } = ServiceApiReadinessResponse.Inconclusive;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "";

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "";

    [JsonPropertyName("matches")]
    public List<ServiceApiReadinessMatch> Matches { get; set; } = [];
}
