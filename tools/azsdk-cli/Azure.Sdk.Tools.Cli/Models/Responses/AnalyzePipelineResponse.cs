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
        var output = $"Failed Tests" + Environment.NewLine +
                     "--------------------------------------------------------------------------------" + Environment.NewLine +
                     FailedTests.ToString() + Environment.NewLine +
                     $"Failed Tasks" + Environment.NewLine +
                     "--------------------------------------------------------------------------------" + Environment.NewLine +
                     FailedTasks.ToString() + Environment.NewLine;
        return ToString(output);
    }
}
