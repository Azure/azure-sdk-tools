using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceApiReadinessMatch
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = "";

    [JsonPropertyName("regions")]
    public List<string> Regions { get; set; } = [];
}
