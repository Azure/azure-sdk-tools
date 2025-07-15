using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse : Response
{
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Summary { get; set; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogEntry> Errors { get; set; } = null;

    [JsonPropertyName("matches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogEntry> Matches { get; set; } = null;

    [JsonPropertyName("suggested_fix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string SuggestedFix { get; set; }

    public override string ToString()
    {
        var output = $"### Summary:" + Environment.NewLine +
                     $"{Summary}" + Environment.NewLine + Environment.NewLine;

        if (Matches?.Count > 0)
        {
            output += $"### Matches:" + Environment.NewLine +
                      $"{string.Join(Environment.NewLine, Matches.Select(m => $"{m.File}:{m.Line} - {m.Message}"))}" +
                      Environment.NewLine + Environment.NewLine;
        }

        if (Errors?.Count > 0)
        {
            output += $"### Suggested Fix:" + Environment.NewLine +
                      $"{SuggestedFix}" + Environment.NewLine + Environment.NewLine +
                      $"### Errors:" + Environment.NewLine +
                      $"{string.Join(Environment.NewLine + Environment.NewLine, Errors.Select(e => $"--> {e.File}:{e.Line}{Environment.NewLine}{e.Message}"))}" +
                      Environment.NewLine;
        }

        return ToString(output);
    }
}

public class LogEntry
{
    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string File { get; set; }
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Url { get; set; }
    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; set; }
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; } = string.Empty;
}