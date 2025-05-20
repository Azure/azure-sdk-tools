using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class FailedTestRunResponse : Response
{
    [JsonPropertyName("run_id")]
    public int RunId { get; set; } = 0;

    [JsonPropertyName("test_case_title")]
    public string TestCaseTitle { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("stack_trace")]
    public string StackTrace { get; set; } = string.Empty;

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    public override string ToString()
    {
        var output = $"### Run ID: {RunId}" + Environment.NewLine +
                     $"### Test Case Title: {TestCaseTitle}" + Environment.NewLine +
                     $"### Outcome: {Outcome}" + Environment.NewLine +
                     $"### URL: {Url}" +
                     $"### Stack Trace:{Environment.NewLine}{StackTrace}" + Environment.NewLine +
                     $"### Error Message:{Environment.NewLine}{ErrorMessage}" + Environment.NewLine;
        return ToString(output);
    }
}
