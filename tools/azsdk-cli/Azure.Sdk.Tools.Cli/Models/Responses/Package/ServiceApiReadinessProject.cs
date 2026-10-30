using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceApiReadinessProject
{
    [JsonPropertyName("source")]
    public ServiceApiReadinessSource Source { get; set; } = new();

    [JsonPropertyName("status")]
    public string ReadinessStatus { get; set; } = ServiceApiReadinessResponse.Inconclusive;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "";

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "";

    [JsonPropertyName("matches")]
    public List<ServiceApiReadinessMatch> Matches { get; set; } = [];
}
