using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
    /// <summary>
    /// Represents additional information to provide to the Azure Knowledge Service.
    /// </summary>
    public class AdditionalInfo
    {
        /// <summary>
        /// The type of the additional information. e.g., "link", "image", "text".
        /// </summary>
        [JsonPropertyName("type")]
        [Required]
        public AdditionalInfoType Type { get; set; }

        /// <summary>
        /// The content of the additional information. It can be a URL, text, or image data depending on the type.
        /// </summary>
        [JsonPropertyName("content")]
        [Required]
        [StringLength(10000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The link to the additional information, required if type is link
        /// </summary>
        [JsonPropertyName("link")]
        [Required]
        [Url]
        public string Link { get; set; } = string.Empty;
    }
}
