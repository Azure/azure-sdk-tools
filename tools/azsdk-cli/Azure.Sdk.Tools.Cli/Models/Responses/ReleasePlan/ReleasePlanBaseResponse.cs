using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan
{
    /// <summary>
    /// Serves as a base class for release plan-related responses, providing common telemetry properties.
    /// </summary>
    public abstract class ReleasePlanBaseResponse : CommandResponse
    {
        [JsonPropertyName("proposed_spec_target")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanSpecTarget? ProposedSpecTarget { get; set; }

        [JsonPropertyName("requires_confirmation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RequiresConfirmation { get; set; }

        [JsonPropertyName("typespec_project")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TypeSpecProject { get; set; } = string.Empty;
        [JsonPropertyName("package_type")]
        public SdkType PackageType { get; set; }
    }
}
