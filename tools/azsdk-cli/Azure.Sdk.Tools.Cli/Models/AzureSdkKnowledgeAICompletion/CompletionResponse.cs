using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    /// <summary>
    /// Represents a response received from the Azure Knowledge Service AI completion API.
    /// </summary>
    public class CompletionResponse
    {
        /// <summary>
        /// The unique ID of the completion API call.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The answer from the Azure Knowledge Service AI.
        /// </summary>
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the Azure Knowledge Service AI was able to provide a result based on the provided context.
        /// </summary>
        [JsonPropertyName("has_result")]
        public bool HasResult { get; set; }

        /// <summary>
        /// The references to the documents used to generate the answer.
        /// </summary>
        [JsonPropertyName("references")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Reference>? References { get; set; }

        /// <summary>
        /// The full context used to generate the answer.
        /// </summary>
        [JsonPropertyName("full_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FullContext { get; set; }

        /// <summary>
        /// The intention of the question
        /// </summary>
        [JsonPropertyName("intention")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QueryIntention? Intention { get; set; }

        /// <summary>
        /// Describe how the LLM reasons through the question to arrive at the final answer
        /// </summary>
        [JsonPropertyName("reasoning")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Reasoning { get; set; }
    }

    public class Reference
    {
        /// <summary>
        /// The title of the document
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The source of the document
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// The link to the document
        /// </summary>
        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// The content of the document
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an error response from the AI completion API.
    /// </summary>
    public class CompletionErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Details { get; set; }
    }
}
