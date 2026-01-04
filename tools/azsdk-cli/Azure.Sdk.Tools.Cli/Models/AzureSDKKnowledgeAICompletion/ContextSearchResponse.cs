using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSDKKnowledgeAICompletion
{
    public class ContextSearchResponse
    {
        [JsonPropertyName("has_result")]
        public bool HasResult { get; set; }

        [JsonPropertyName("knowledges")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Knowledge>? Knowledges { get; set; }
    }
}
