using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Commands;

public abstract class CommandResponse
{
    private static string SerializePropertiesPlainText(object obj, int indentLevel)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        var type = obj.GetType();
        var properties = type.GetProperties()
            .Where(p => Attribute.IsDefined(p, typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute)))
            .ToArray();
        var indent = new string(' ', indentLevel * 4);
        var result = "";

        foreach (var prop in properties)
        {
            var name = prop.Name;
            var value = prop.GetValue(obj);

            result += $"{indent}{name}\n";
            result += $"{indent}=========\n";

            if (value != null && value is not string && !value.GetType().IsPrimitive)
            {
                result += SerializePropertiesPlainText(value, indentLevel + 1);
            }
            else
            {
                result += $"{indent}{(value ?? "null")}\n";
            }
        }

        return result;
    }

    private readonly JsonSerializerOptions compressSerializerOptions = new()
    {
        WriteIndented = false,
    };

    private readonly JsonSerializerOptions prettySerializerOptions = new()
    {
        WriteIndented = true,
    };

    public string AsPlainText()
    {
        return SerializePropertiesPlainText(this, 0);
    }

    public string AsJson()
    {
        return JsonSerializer.Serialize(this, prettySerializerOptions);
    }

    public string AsMcp()
    {
        return JsonSerializer.Serialize(this, compressSerializerOptions);
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, prettySerializerOptions);
    }
}