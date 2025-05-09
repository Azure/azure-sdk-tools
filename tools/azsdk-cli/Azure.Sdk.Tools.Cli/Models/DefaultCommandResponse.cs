using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse() : BaseCommandResponse()
{
    [JsonPropertyName("exitcode")]
    public int ExitCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    public override string ToPlainText()
    {
        return $"Exit Code: {ExitCode}\n" +
               $"Message: {Message}\n" +
               $"Result: {Result?.ToString() ?? "null"}\n" +
               $"Duration: {Duration}ms";
    }
}