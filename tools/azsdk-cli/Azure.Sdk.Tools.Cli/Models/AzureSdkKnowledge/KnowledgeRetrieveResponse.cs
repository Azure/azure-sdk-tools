using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledge
{
    public class KnowledgeRetrieveResponse
    {
        [JsonPropertyName("has_result")]
        public bool HasResult { get; set; }

        [JsonPropertyName("knowledge_list")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Knowledge>? Knowledges { get; set; }
    }
}
