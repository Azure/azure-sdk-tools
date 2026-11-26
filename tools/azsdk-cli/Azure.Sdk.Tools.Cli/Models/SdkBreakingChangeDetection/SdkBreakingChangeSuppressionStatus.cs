using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection
{
    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<SdkBreakingChangeSuppressionStatus>))]
    public enum SdkBreakingChangeSuppressionStatus
    {
        [EnumMember(Value = "none")]
        None,
        [EnumMember(Value = "suppressed")]
        Suppressed,
        [EnumMember(Value = "partiallySuppressed")]
        PartiallySuppressed,
    }
}
