using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIViewWeb.Models;

public class MentionRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; }
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; }
    [JsonPropertyName("code")]
    public string Code { get; set; }
    [JsonPropertyName("comments")]
    public List<ApiViewComment> Comments { get; set; } 
}
