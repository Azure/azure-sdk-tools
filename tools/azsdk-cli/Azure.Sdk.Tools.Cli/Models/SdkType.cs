using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SdkType
{
    Unknown,
    [JsonStringEnumMemberName("mgmt")]
    Management,
    [JsonStringEnumMemberName("client")]
    Dataplane,
    [JsonStringEnumMemberName("spring")]
    Spring,
    [JsonStringEnumMemberName("functions")]
    Functions
}
