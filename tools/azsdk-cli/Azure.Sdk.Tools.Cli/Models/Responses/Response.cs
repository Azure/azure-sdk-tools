using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class Response : JsonConverter<Response>
{
    [JsonPropertyName("response_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseError { get; set; }

    [JsonPropertyName("response_errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> ResponseErrors { get; set; }

    protected string ToString(string value)
    {
        List<string> errors = [];
        if (!string.IsNullOrEmpty(ResponseError))
        {
            errors.Add("[ERROR] " + ResponseError);
        }
        foreach (var error in ResponseErrors ?? [])
        {
            errors.Add("[ERROR] " + error);
        }

        if (errors.Count > 0)
        {
            value = string.Join(Environment.NewLine, errors) + Environment.NewLine;
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
