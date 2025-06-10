using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse : Response
{
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Summary { get; set; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogError> Errors { get; set; } = [];

    [JsonPropertyName("suggested_fix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string SuggestedFix { get; set; }

    public override string ToString()
    {
        var output = $"### Summary:" + Environment.NewLine +
                     $"{Summary}" + Environment.NewLine + Environment.NewLine +
                     $"### Suggested Fix:" + Environment.NewLine +
                     $"{SuggestedFix}" + Environment.NewLine +
                     $"{Environment.NewLine}### Errors:{Environment.NewLine}{string.Join(Environment.NewLine, Errors.Select(e => $"{e.File}:{e.Line} - {e.Message}"))}" + Environment.NewLine;
        return ToString(output);
    }
}

public class LogError
{
    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string File { get; set; } = string.Empty;
    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; set; }
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; } = string.Empty;
}