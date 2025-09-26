using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class APIViewResponse : CommandResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = false;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    public override string ToString()
    {
        var output = new StringBuilder();

        output.AppendLine(Success ? "Operation completed successfully" : "Operation failed");

        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }

        return ToString(output);
    }
}
