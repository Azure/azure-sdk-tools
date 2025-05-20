using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class AnalyzePipelineResponse : Response
{
    [JsonPropertyName("failed_tests")]
    public List<FailedTestRunResponse> FailedTests { get; set; } = [];

    [JsonPropertyName("failed_tasks")]
    public List<LogAnalysisResponse> FailedTasks { get; set; } = [];

    [JsonPropertyName("pipeline_url")]
    public string PipelineUrl { get; set; } = string.Empty;

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
