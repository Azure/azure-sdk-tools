using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

// Basic helper response class for when object types should always be output as JSON
public class ObjectCommandResponse : CommandResponse
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    protected override string Format()
    {
        var result = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            result.AppendLine(Message);
        }
        if (Result != null)
        {
            result.AppendLine(JsonSerializer.Serialize(Result, serializerOptions));
        }
        return result.ToString();
    }
}
