using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public enum SdkLanguage
{
    [JsonPropertyName(".NET")]
    DotNet,
    [JsonPropertyName("Java")]
    Java,
    [JsonPropertyName("JavaScript")]
    JavaScript,
    [JsonPropertyName("Python")]
    Python,
    [JsonPropertyName("Go")]
    Go,
}
