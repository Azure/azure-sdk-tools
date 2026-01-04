using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSDKKnowledgeAICompletion
{
    public class Knowledge
    {
        [JsonPropertyName("document_source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("document_filename")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("document_title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("document_link")]
        public string Link { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
