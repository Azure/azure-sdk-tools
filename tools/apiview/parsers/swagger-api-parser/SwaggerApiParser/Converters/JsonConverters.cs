using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwaggerApiParser.Converters
{
    public class SecurityConverter<T> : JsonConverter<List<Dictionary<T, List<T>>>>
    {
        public override List<Dictionary<T, List<T>>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var element = JsonDocument.ParseValue(ref reader).RootElement;
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException($"Invalid JSON value for {typeToConvert}: {element}");
            }
            var result = new List<Dictionary<T, List<T>>>();

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException($"Invalid JSON value for {typeToConvert}: {item}");
                }
                var dictionary = new Dictionary<T, List<T>>();

                foreach (var property in item.EnumerateObject())
                {
                    var key = JsonSerializer.Deserialize<T>(property.Name, options);
                    var value = JsonSerializer.Deserialize<List<T>>(property.Value.GetRawText(), options);
                    if (value.Count > 0)
                    {
                        dictionary.Add(key, value);
                    }
                }
                if (dictionary.Count > 0)
                {
                    result.Add(dictionary);
                }
            }

            if (result.Count > 0)
                return result;

            return null;
        }

        public override void Write(Utf8JsonWriter writer, List<Dictionary<T, List<T>>> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var dictionary in value)
            {
                if (dictionary.Any(pair => pair.Value == null || pair.Value.Count == 0))
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStartObject();
                    foreach (var pair in dictionary)
                    {
                        writer.WritePropertyName(JsonSerializer.Serialize(pair.Key, options));
                        JsonSerializer.Serialize(writer, pair.Value, options);
                    }
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
        }

    }
}
