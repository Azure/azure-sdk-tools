using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class FailedTestRunResponse : Response
{
    [JsonPropertyName("run_id")]
    public int RunId { get; set; } = 0;

    [JsonPropertyName("test_case_title")]
    public string TestCaseTitle { get; set; }

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; }

    [JsonPropertyName("stack_trace")]
    public string StackTrace { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

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
