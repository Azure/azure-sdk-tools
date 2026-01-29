using System.Text.Json.Serialization;

namespace Sdk.Tools.Cli.Models;

public class GeneratedSample
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("description")]
    public required string Description { get; set; }
    
    [JsonPropertyName("code")]
    public required string Code { get; set; }
    
    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
}
