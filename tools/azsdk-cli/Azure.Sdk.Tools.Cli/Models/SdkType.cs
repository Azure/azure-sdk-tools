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

public static class SdkTypeExtensions
{
    public static bool IsDataPlane(this SdkType sdkType) =>
        sdkType is SdkType.Dataplane or SdkType.Spring or SdkType.Functions;

}
