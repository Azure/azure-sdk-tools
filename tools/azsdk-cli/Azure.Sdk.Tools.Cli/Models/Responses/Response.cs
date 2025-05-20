using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class Response : JsonConverter<Response>
{
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseError { get; set; }

    protected string ToString(string value)
    {
        if (!string.IsNullOrEmpty(ResponseError))
        {
            return "[ERROR] " + ResponseError;
        }

        return value;
    }

    public override Response? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<Response>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, Response value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.ResponseError != null)
        {
            writer.WriteString("error", value.ResponseError);
        }
        else
        {
            JsonSerializer.Serialize(writer, this, options);
        }
        writer.WriteEndObject();
    }
}
