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
    public static string ToApiReviewPackageType(this SdkType sdkType) => sdkType switch
    {
        SdkType.Management or SdkType.Unknown => "mgmt",
        SdkType.Dataplane or SdkType.Spring or SdkType.Functions => "client",
        _ => throw new ArgumentException($"Unsupported SDK type '{sdkType}'.", nameof(sdkType))
    };
}
