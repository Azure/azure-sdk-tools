using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceApiReadinessSource
{
    [JsonPropertyName("packagePath")]
    public string PackagePath { get; set; } = "";

    [JsonPropertyName("configuration")]
    public string Configuration { get; set; } = "";

    [JsonPropertyName("commitSha")]
    public string CommitSha { get; set; } = "";

    [JsonPropertyName("specRepository")]
    public string SpecRepository { get; set; } = "";

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "";

    [JsonPropertyName("expectedNamespace")]
    public string ExpectedNamespace { get; set; } = "";

    [JsonPropertyName("metadataFiles")]
    public List<string> MetadataFiles { get; set; } = [];
}
