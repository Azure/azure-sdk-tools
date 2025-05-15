using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse()
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    public override string ToString()
    {
        return $"Message: {Message}\n" +
               $"Result: {Result?.ToString() ?? "null"}\n" +
               $"Duration: {Duration}ms";
    }
}