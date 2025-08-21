using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.Tools.Cli.Models.AiCompletion
{
  public class AdditionalInfo
  {
    [JsonPropertyName("type")]
    [Required]
    public AdditionalInfoType Type { get; set; }

    [JsonPropertyName("content")]
    [Required]
    [StringLength(5000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    [Required]
    [Url]
    [StringLength(2000, MinimumLength = 1)]
    public string Link { get; set; } = string.Empty;
  }
}
