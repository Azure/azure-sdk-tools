using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse()
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<LogError> Errors { get; set; } = [];

    [JsonPropertyName("suggestedfix")]
    public string SuggestedFix { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"### Summary:\n" +
               $"{Summary}\n" +
               $"\n### Suggested Fix:\n" +
               $"{SuggestedFix}\n" +
               $"\n### Errors:\n{string.Join("\n", Errors.Select(e => $"{e.File}:{e.Line} - {e.Message}"))}\n";
    }
}

public class LogError
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;
    [JsonPropertyName("line")]
    public int Line { get; set; }
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}