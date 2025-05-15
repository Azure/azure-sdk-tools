using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public List<LogError> Errors { get; set; } = [];

    [JsonPropertyName("suggested_fix")]
    public string SuggestedFix { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"### Summary:" + Environment.NewLine +
               $"{Summary}" + Environment.NewLine + Environment.NewLine +
               $"### Suggested Fix:" + Environment.NewLine +
               $"{SuggestedFix}" + Environment.NewLine +
               $"{Environment.NewLine}### Errors:{Environment.NewLine}{string.Join(Environment.NewLine, Errors.Select(e => $"{e.File}:{e.Line} - {e.Message}"))}" + Environment.NewLine;
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