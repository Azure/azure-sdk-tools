using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.SecretRotation.Configuration;

public class StoreConfiguration
{
    [JsonPropertyName("type")] 
    public string? Type { get; set; }

    [JsonPropertyName("parameters")] 
    public JsonObject? Parameters { get; set; }

    [JsonPropertyName("isOrigin")] 
    public bool IsOrigin { get; set; }

    [JsonPropertyName("isPrimary")] 
    public bool IsPrimary { get; set; }

    [JsonPropertyName("name")] 
    public string? Name { get; set; }

    public string ResolveStoreName(string configurationKey)
    {
        return Name ?? $"{configurationKey} ({Type})";
    }
}
