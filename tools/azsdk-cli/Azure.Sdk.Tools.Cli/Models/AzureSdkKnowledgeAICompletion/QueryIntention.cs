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
        [JsonPropertyName("scope")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scope { get; set; }
    }
}
