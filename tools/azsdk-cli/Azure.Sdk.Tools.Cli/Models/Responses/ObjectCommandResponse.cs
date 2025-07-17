using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

// Basic helper response class for when object types should always be output as JSON
public class ObjectCommandResponse : Response
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    public override string ToString()
    {
        var output = JsonSerializer.Serialize(Result, serializerOptions);
        return ToString(output);
    }
}
