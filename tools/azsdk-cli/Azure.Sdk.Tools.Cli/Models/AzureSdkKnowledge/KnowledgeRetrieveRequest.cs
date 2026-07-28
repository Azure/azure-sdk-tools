using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledge
{
    public class KnowledgeRetrieveRequest
    {
        /// <summary>
        /// The tenant ID of Azure Knowledge Service to use for this request.
        /// </summary>
        [JsonPropertyName("tenant_id")]
        public AzureSdkKnowledgeServiceTenant AzureSdkKnowledgeServiceTenant { get; set; } = AzureSdkKnowledgeServiceTenant.AzureTypespecAuthoring;

        /// <summary>
        /// The query to ask the AI.
        /// </summary>
        [JsonPropertyName("query")]
        [Required]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 10000 characters")]
        public string Query { get; set; }

        /// <summary>
        /// The user ID of the person making the request.
        /// </summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        /// <summary>
        /// The search mode to use for the request. This can be quick or deep.
        /// </summary>
        [JsonPropertyName("search_mode")]
        public string? SearchMode {  get; set; }

        /// <summary>
        /// The knowledgesources to search.
        /// </summary>
        [JsonPropertyName("sources")]
        public List<string>? Sources { get; set; }

        /// <summary>
        /// The maximum number of top-ranked results to return.
        /// </summary>
        [JsonPropertyName("top_k")]
        public int? TopK { get; set; }
    }
}
