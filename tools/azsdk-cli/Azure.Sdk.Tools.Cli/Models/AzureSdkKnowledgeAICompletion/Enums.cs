using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<AzureSdkKnowledgeServiceTenant>))]
    public enum AzureSdkKnowledgeServiceTenant
    {
        [EnumMember(Value = "azure_typespec_authoring")]
        AzureTypespecAuthoring
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
        Image,

        [EnumMember(Value = "text")]
        Text,

    }

    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<QuestionScope>))]
    public enum QuestionScope
    {
        [EnumMember(Value = "unknown")]
        Unknown,

        [EnumMember(Value = "branded")]
        Branded,

        [EnumMember(Value = "unbranded")]
        Unbranded,
    }
}
