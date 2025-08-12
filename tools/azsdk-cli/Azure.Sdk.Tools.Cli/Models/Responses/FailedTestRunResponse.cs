using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class FailedTestRunResponse : Response
{
    [JsonPropertyName("run_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RunId { get; set; } = 0;

    [JsonPropertyName("test_case_title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string TestCaseTitle { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string ErrorMessage { get; set; }

    [JsonPropertyName("stack_trace")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string StackTrace { get; set; }

    [JsonPropertyName("outcome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Outcome { get; set; }

    [JsonPropertyName("uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Uri { get; set; }

    public override string ToString()
    {
        var output = "";
        output += $"## {TestCaseTitle}{Environment.NewLine}";
        if (RunId != 0)
        {
            output += $"Run ID: {RunId}{Environment.NewLine}";
        }
        output += $"Outcome: {Outcome}{Environment.NewLine}";
        if (!string.IsNullOrEmpty(Uri))
        {
            output += $"URI: {Uri}{Environment.NewLine}";
        }
        output += $"{Environment.NewLine}### Stack Trace{Environment.NewLine}{StackTrace}{Environment.NewLine}";
        output += $"### Error Message{Environment.NewLine}{ErrorMessage}{Environment.NewLine}";

        return ToString(output);
    }
}
