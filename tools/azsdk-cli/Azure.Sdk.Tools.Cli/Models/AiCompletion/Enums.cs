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

    [JsonConverter(typeof(JsonStringEnumWithEnumMemberConverter<Source>))]
    public enum Source
    {
        [EnumMember(Value = "typespec_docs")]
        TypeSpec,

        [EnumMember(Value = "typespec_azure_docs")]
        TypeSpecAzure,

        [EnumMember(Value = "azure_rest_api_specs_wiki")]
        AzureRestAPISpec,

        [EnumMember(Value = "azure_sdk_for_python_docs")]
        AzureSDKForPython,

        [EnumMember(Value = "azure_sdk_for_python_wiki")]
        AzureSDKForPythonWiki,

        [EnumMember(Value = "static_typespec_qa")]
        TypeSpecQA,

        [EnumMember(Value = "azure_api_guidelines")]
        AzureAPIGuidelines,

        [EnumMember(Value = "azure_resource_manager_rpc")]
        AzureResourceManagerRPC,

        [EnumMember(Value = "static_typespec_migration_docs")]
        TypeSpecMigration,

        [EnumMember(Value = "azure-sdk-docs-eng")]
        AzureSDKDocsEng,

        [EnumMember(Value = "azure-sdk-guidelines")]
        AzureSDKGuidelines,

        [EnumMember(Value = "typespec_azure_http_specs")]
        TypeSpecAzureHttpSpecs,

        [EnumMember(Value = "typespec_http_specs")]
        TypeSpecHttpSpecs
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
