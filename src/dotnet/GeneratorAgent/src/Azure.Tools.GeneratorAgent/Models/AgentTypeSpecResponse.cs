using System.Text.Json.Serialization;

namespace Azure.Tools.GeneratorAgent.Models
{
    /// <summary>
    /// Represents the parsed response from the AI agent containing updated TypeSpec content
    /// </summary>
    internal class AgentTypeSpecResponse
    {
        /// <summary>
        /// File path from the response (should be "client.tsp")
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Plain text content of the updated client.tsp file
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
