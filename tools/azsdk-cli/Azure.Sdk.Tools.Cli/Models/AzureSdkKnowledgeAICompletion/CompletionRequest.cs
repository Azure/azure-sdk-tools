using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    /// <summary>
    /// Represents a chat completion request to Azure Knowledge Service.
    /// </summary>
    public class CompletionRequest
    {
        /// <summary>
        /// The tenant ID of Azure Knowledge Service to use for this request.
        /// </summary>
        [JsonPropertyName("tenant_id")]
        public AzureSdkKnowledgeServiceTenant AzureSdkKnowledgeServiceTenant { get; set; } = AzureSdkKnowledgeServiceTenant.AzureTypespecAuthoring;

        /// <summary>
        /// The message to send to Azure Knowledge Service
        /// </summary>
        [JsonPropertyName("message")]
        [Required]
        public Message Message { get; set; } = new();

        /// <summary>
        /// Additional information to provide to the Azure Knowledge Service
        /// </summary>
        [JsonPropertyName("additional_infos")]
        public List<AdditionalInfo> AdditionalInfos { get; set; } = new();
    }
}
