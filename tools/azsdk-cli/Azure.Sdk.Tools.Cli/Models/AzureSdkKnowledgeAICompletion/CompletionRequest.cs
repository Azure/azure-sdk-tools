using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    public class CompletionRequest
    {
        [JsonPropertyName("tenant_id")]
        public AzureSdkKnowledgeServiceTenant TenantId { get; set; } = AzureSdkKnowledgeServiceTenant.AzureTypespecAuthoring;

        [JsonPropertyName("message")]
        [Required]
        public Message Message { get; set; } = new();

        [JsonPropertyName("additional_infos")]
        public List<AdditionalInfo> AdditionalInfos { get; set; } = new();
    }
}
