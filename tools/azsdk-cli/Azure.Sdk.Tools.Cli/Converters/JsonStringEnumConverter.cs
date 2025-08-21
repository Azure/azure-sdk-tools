using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace Azure.Sdk.Tools.Cli.Converters
{
  /// <summary>
  /// Custom JSON converter for enums that supports EnumMember attribute values.
  /// </summary>
  /// <typeparam name="T">The enum type</typeparam>
  public class JsonStringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
  {
    private static readonly Dictionary<T, string> EnumToString = new();
    private static readonly Dictionary<string, T> StringToEnum = new();

    static JsonStringEnumConverter()
    {
      var enumType = typeof(T);
      var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

      foreach (var field in fields)
      {
        var enumValue = (T)field.GetValue(null)!;
        var memberAttribute = field.GetCustomAttribute<EnumMemberAttribute>();
        var stringValue = memberAttribute?.Value ?? enumValue.ToString().ToLowerInvariant();

        EnumToString[enumValue] = stringValue;
        StringToEnum[stringValue] = enumValue;
      }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.String)
      {
        throw new JsonException($"Expected string token for enum {typeof(T).Name}, got {reader.TokenType}");
      }

      var value = reader.GetString();
      if (string.IsNullOrEmpty(value))
      {
        return default;
      }

      if (StringToEnum.TryGetValue(value, out var result))
      {
        return result;
      }

      // Fallback to case-insensitive matching
      var match = StringToEnum.FirstOrDefault(kvp =>
          string.Equals(kvp.Key, value, StringComparison.OrdinalIgnoreCase));

      if (!match.Equals(default(KeyValuePair<string, T>)))
      {
        return match.Value;
      }

      throw new JsonException($"Unable to convert \"{value}\" to enum {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
      if (EnumToString.TryGetValue(value, out var stringValue))
      {
        writer.WriteStringValue(stringValue);
      }
      else
      {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
      }
    }
  }

  /// <summary>
  /// Custom JSON converter for lists of enums.
  /// </summary>
  /// <typeparam name="T">The enum type</typeparam>
  public class JsonStringEnumListConverter<T> : JsonConverter<List<T>> where T : struct, Enum
  {
    private readonly JsonStringEnumConverter<T> _enumConverter = new();

    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
      {
        return null;
      }

      var list = new List<T>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray)
        {
          break;
        }

        list.Add(_enumConverter.Read(ref reader, typeof(T), options));
      }

      return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
      writer.WriteStartArray();
      foreach (var item in value)
      {
        _enumConverter.Write(writer, item, options);
      }
      writer.WriteEndArray();
    }
  }
}
