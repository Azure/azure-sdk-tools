using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class APIViewResponse : CommandResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("language")]
    public object? Language { get; set; }

    [JsonPropertyName("package_name")]
    public object? PackageName { get; set; }

    protected override string Format()
    {
        var output = new StringBuilder();

        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }

        if (Result == null)
        {
            return output.ToString();
        }

        string resultString = Result.ToString()!;

        // Check if the result is a JSON string (starts and ends with quotes)
        if (resultString.StartsWith("\"") && resultString.EndsWith("\""))
        {
            try
            {
                string? parsed = JsonSerializer.Deserialize<string>(resultString);
                if (parsed != null)
                {
                    output.AppendLine(parsed);
                    return output.ToString();
                }
            }
            catch (JsonException)
            {
            }
        }
     
        output.AppendLine(resultString);
        return output.ToString();
    }
}
