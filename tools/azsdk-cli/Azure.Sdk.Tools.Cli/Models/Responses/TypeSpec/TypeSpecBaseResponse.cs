using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    public class TypeSpecBaseResponse
    {
        [Telemetry]
        [JsonPropertyName("typespec_project")]
        public string? TypeSpecProject { get; set; }
        [JsonPropertyName("package_type")]
        [Telemetry]
        public SdkType PackageType { get; set; }
        }
}
