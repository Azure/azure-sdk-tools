using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.SecretRotation.Configuration;

public class StoreConfiguration
{
    public string? Type { get; set; }

    public JsonObject? Parameters { get; set; }

    public bool IsOrigin { get; set; }

    public bool IsPrimary { get; set; }

    public string? Name { get; set; }

    public bool UpdateAfterPrimary { get; set; }

    public string ResolveStoreName(string configurationKey)
    {
        return Name ?? $"{configurationKey} ({Type})";
    }
}
