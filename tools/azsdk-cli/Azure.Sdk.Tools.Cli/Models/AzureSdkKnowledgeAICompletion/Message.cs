using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    /// <summary>
    /// Represents a message in the AI chat conversation.
    /// </summary>
    public class Message
    {
        /// <summary>
        /// The role of the AI chat message, e.g., "system", "user", or "assistant".
        /// </summary>
        [JsonPropertyName("role")]
        [Required]
        public Role Role { get; set; }

        /// <summary>
        /// The content of the AI chat message
        /// </summary>
        [JsonPropertyName("content")]
        [Required]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The name of the AI chat message, used for system messages
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
    }
}
