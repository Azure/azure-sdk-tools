using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.seraialize;

namespace Azure.Sdk.Tools.Cli.Models.AiCompletion
{
    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<TenantId>))]
    public enum TenantId
    {
        [EnumMember(Value = "azure_sdk_qa_bot")]
        AzureSDKQaBot,

        [EnumMember(Value = "typespec_extension")]
        TypeSpecExtension,

        [EnumMember(Value = "python_channel_qa_bot")]
        PythonChannelQaBot,

        [EnumMember(Value = "azure_sdk_onboarding")]
        AzureSDKOnboarding
    }

    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<Role>))]
    public enum Role
    {
        [EnumMember(Value = "user")]
        User,

        [EnumMember(Value = "assistant")]
        Assistant,

        [EnumMember(Value = "system")]
        System
    }

    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<AdditionalInfoType>))]
    public enum AdditionalInfoType
    {
        [EnumMember(Value = "link")]
        Link,

        [EnumMember(Value = "image")]
        Image
    }

    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<QuestionScope>))]
    public enum QuestionScope
    {
        [EnumMember(Value = "unknown")]
        Unknown,

        [EnumMember(Value = "branded")]
        Branded,

        [EnumMember(Value = "unbranded")]
        Unbranded
    }
}
