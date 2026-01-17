using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion
{
  public class Message
  {
    [JsonPropertyName("role")]
    [Required]
    public Role Role { get; set; }

    [JsonPropertyName("content")]
    [Required]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 10000 characters")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("raw_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(50000)]
    public string? RawContent { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [StringLength(100)]
    public string? Name { get; set; }
  }
}
