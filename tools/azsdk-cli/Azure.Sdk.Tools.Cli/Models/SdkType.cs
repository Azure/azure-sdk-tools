using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public enum SdkType
{
    [JsonPropertyName("")]
    Unknown,
    [JsonPropertyName("mgmt")]
    Management,
    [JsonPropertyName("client")]
    Dataplane,
    [JsonPropertyName("spring")]
    Spring
}
