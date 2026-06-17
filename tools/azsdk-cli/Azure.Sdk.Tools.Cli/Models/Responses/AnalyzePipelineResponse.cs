using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class AnalyzePipelineResponse : CommandResponse
{
    [JsonPropertyName("failed_test_titles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, List<string>> FailedTests { get; set; } = [];

    [JsonPropertyName("failed_tasks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<LogAnalysisResponse> FailedTasks { get; set; } = [];

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
    };

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (FailedTests.Count > 0)
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("Failed Tests");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(JsonSerializer.Serialize(FailedTests, jsonOptions));
        }

        if (FailedTasks.Count > 0)
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("Failed Tasks");
            sb.AppendLine("--------------------------------------------------------------------------------");
            foreach (var task in FailedTasks)
            {
                sb.AppendLine(task.ToString());
                sb.AppendLine("--------------------------------------------------------------------------------");
            }
        }

        if (FailedTests.Count == 0 && FailedTasks.Count == 0)
        {
            sb.AppendLine("");
            sb.AppendLine("No failures found");
        }

        return sb.ToString();
    }
}
