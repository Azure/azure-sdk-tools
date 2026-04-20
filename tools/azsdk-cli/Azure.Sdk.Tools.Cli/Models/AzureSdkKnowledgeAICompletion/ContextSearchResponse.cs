using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;

namespace Azure.Sdk.Tools.Cli.Models.AzureSDKKnowledgeAICompletion
{
    public class ContextSearchResponse
    {
        [JsonPropertyName("has_result")]
        public bool HasResult { get; set; }

        [JsonPropertyName("knowledges")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Knowledge>? Knowledges { get; set; }

        [JsonPropertyName("intention")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntention Intention { get; set; }
    }
}
