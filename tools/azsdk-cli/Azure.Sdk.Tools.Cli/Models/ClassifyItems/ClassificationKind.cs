using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<ClassificationKind>))]
    public enum ClassificationKind
    {
        [EnumMember(Value = "sdk-breaking-change")]
        SdkBreakingChange,

        [EnumMember(Value = "customization")]
        Customization,

        [EnumMember(Value = "unknown")]
        Unknown,

    }
}
