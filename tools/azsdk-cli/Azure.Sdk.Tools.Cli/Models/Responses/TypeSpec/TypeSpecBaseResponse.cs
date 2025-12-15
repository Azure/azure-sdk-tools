using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    /// <summary>
    /// Serves as a base class for TypeSpec-related responses, providing common telemetry properties.
    /// </summary>
    public abstract class TypeSpecBaseResponse : CommandResponse
    {
        [Telemetry]
        [JsonPropertyName("typespec_project")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public required string TypeSpecProject { get; set; } = string.Empty;
        [JsonPropertyName("package_type")]
        [Telemetry]
        public SdkType PackageType { get; set; }
    }
}
