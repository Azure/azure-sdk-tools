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

        if (IsValidJson(resultString))
        {
            output.AppendLine(resultString);
        }
        else
        {
            string unescapedResult = Regex.Unescape(resultString);
            output.AppendLine(unescapedResult);
        }

        return output.ToString();
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
