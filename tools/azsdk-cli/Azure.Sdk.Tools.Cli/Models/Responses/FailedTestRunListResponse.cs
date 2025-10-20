using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class FailedTestRunListResponse : CommandResponse
{
    [JsonPropertyName("failed_test_runs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<FailedTestRunResponse> Items { get; set; } = [];

    public override string ToString()
    {
        var output = new StringBuilder();
        foreach (var run in Items)
        {
            output.AppendLine(run.ToString());
        }
        return ToString(output);
    }
}
