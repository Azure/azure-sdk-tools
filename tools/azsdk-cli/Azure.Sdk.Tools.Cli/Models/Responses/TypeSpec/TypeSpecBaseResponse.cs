using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec
{
    /// <summary>
    /// Serves as a base class for TypeSpec-related responses, providing common telemetry properties.
    /// </summary>
    public abstract class TypeSpecBaseResponse : CommandResponse
    {
        [JsonPropertyName("typespec_project")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public required string TypeSpecProject { get; set; } = string.Empty;
        [JsonPropertyName("package_type")]
        public SdkType PackageType { get; set; }
        [JsonPropertyName("language")]
        public virtual string Language { get; set; } = "TypeSpec";
    }
}
