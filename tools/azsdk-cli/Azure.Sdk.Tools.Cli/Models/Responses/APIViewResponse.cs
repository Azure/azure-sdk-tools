using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class APIViewResponse : CommandResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = false;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }


    [JsonPropertyName("result")]
    public object? Result { get; set; }

    protected override string Format()
    {
        var output = new StringBuilder();

        output.AppendLine(Success ? "✓ Operation completed successfully" : "✗ Operation failed");

        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }
      
        if (!string.IsNullOrWhiteSpace(Content))
        {
            string unescapedContent = System.Text.RegularExpressions.Regex.Unescape(Content);
            output.AppendLine(unescapedContent);
        }

        if (Result != null)
        {
            output.AppendLine(Result.ToString());
        }

        return output.ToString();
    }
}
