using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    /// <summary>
    /// Represents an intention of a query analyzed by the Azure Knowledge Service.
    /// </summary>
    public class QueryIntention
    {
        /// <summary>
        /// The question to ask the AI.
        /// </summary>
        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// The category of the question, e.g. versioning
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// The scope of the question, e.g. branded, unbranded
        /// </summary>
        [JsonPropertyName("question_scope")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QuestionScope? QuestionScope { get; set; }

        /// <summary>
        /// The service type the question pertains to, e.g. management-plane, data-plane
        /// </summary>
        [JsonPropertyName("service_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ServiceType? ServiceType { get; set; }
    }
}
