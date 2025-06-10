using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class AnalyzePipelineResponse : Response
{
    [JsonPropertyName("failed_tests")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FailedTestRunResponse> FailedTests { get; set; } = [];

    [JsonPropertyName("failed_tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LogAnalysisResponse> FailedTasks { get; set; } = [];

    [JsonPropertyName("pipeline_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string PipelineUrl { get; set; }

    public override string ToString()
    {
        var output = "";

        if (FailedTests.Count > 0)
        {
            output += "--------------------------------------------------------------------------------" + Environment.NewLine +
                      $"Failed Tests" + Environment.NewLine +
                      "--------------------------------------------------------------------------------" + Environment.NewLine;
            output += string.Join(Environment.NewLine, FailedTests.Select(t => t.ToString())) + Environment.NewLine;
        }

        if (FailedTasks.Count > 0)
        {
            output += "--------------------------------------------------------------------------------" + Environment.NewLine +
                      $"Failed Tasks" + Environment.NewLine +
                      "--------------------------------------------------------------------------------" + Environment.NewLine;
            output += string.Join(Environment.NewLine, FailedTasks.Select(t => t.ToString())) + Environment.NewLine;
        }

        return ToString(output);
    }
}
