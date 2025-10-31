using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypeSpecBaseResponse
    {
        [Telemetry]
        [JsonPropertyName("typeSpecProject")]
        public string? TypeSpecProject { get; set; }
        [JsonPropertyName("packageType")]
        [Telemetry]
        public SdkType PackageType { get; set; }
        }
}
