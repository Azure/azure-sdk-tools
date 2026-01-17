using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSDKKnowledgeAICompletion
{
    public class DocumentReference
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;

        [JsonPropertyName("snippet")]
        public string Snippet { get; set; } = string.Empty;
    }
}
