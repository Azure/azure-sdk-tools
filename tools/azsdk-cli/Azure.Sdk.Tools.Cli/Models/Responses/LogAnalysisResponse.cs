using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse : CommandResponse
{
    public bool HasErrors => Errors != null && Errors.Count > 0;

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogEntry> Errors { get; set; } = null;

    [JsonPropertyName("pipeline_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PipelineUrl { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (Errors?.Count > 0)
        {
            sb.AppendLine($"### Errors:");
            sb.AppendLine(string.Join(Environment.NewLine + Environment.NewLine, Errors.Select(e => $"--> {e.File}:{e.Line}{Environment.NewLine}{e.Message}")));
        }

        if (!string.IsNullOrEmpty(PipelineUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"### Pipeline: {PipelineUrl}");
        }

        if (Errors?.Count == 0)
        {
            sb.AppendLine("No errors found in pipeline logs.");
        }

        return sb.ToString();
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
