using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Serialization
{
    public class JsonStringEnumWithEnumMemberConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            foreach (var field in typeof(T).GetFields())
            {
                var attr = field.GetCustomAttribute<EnumMemberAttribute>();
                if (attr?.Value == value)
                {
                    var ret = field.GetValue(null);
                    return ret != null ? (T)ret : default;
                }
            }
            return Enum.Parse<T>(value);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var field = typeof(T).GetField(value.ToString());
            if (field == null)
            {
                writer.WriteStringValue(value.ToString());
            } else 
            {
                var attr = field.GetCustomAttribute<EnumMemberAttribute>();
                writer.WriteStringValue(attr?.Value ?? field.Name);
            }
        }
    }
}
