using System.Text.Json;
using Newtonsoft.Json;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Models;

public class LogAnalysisResponse() : CommandResponse
{
    [JsonProperty()]
    public string Summary { get; set; } = string.Empty;
    [JsonProperty()]
    public List<LogError> Errors { get; set; } = [];
    [JsonProperty()]
    public string SuggestedFix { get; set; } = string.Empty;
}

public class LogError
{
    [JsonProperty()]
    public string File { get; set; } = string.Empty;
    [JsonProperty()]
    public int Line { get; set; }
    [JsonProperty()]
    public string Message { get; set; } = string.Empty;
}