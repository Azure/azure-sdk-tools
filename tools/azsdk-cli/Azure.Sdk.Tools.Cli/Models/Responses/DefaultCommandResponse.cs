using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse : Response
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
        return $"Message: {Message}" + Environment.NewLine +
               $"Result: {Result?.ToString() ?? "null"}" + Environment.NewLine +
               $"Duration: {Duration}ms";
    }
}