using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class RoleBasedAccessControlsConfig : BaseConfig
{
    [JsonRequired, JsonPropertyName("role"), JsonPropertyOrder(0)]
    public string? Role { get; set; }

    [JsonRequired, JsonPropertyName("scope"), JsonPropertyOrder(1)]
    public string? Scope { get; set; }

    public string ToIndentedString(int indentLevel = 0)
    {
        var indent = "";
        foreach (var lvl in Enumerable.Range(0, indentLevel))
        {
            indent += "    ";
        }

        var sb = new StringBuilder();
        sb.AppendLine(indent + $"Role: {Role}");
        sb.AppendLine(indent + $"Scope: {Scope}");
        return sb.ToString();
    }

    public override string ToString()
    {
        return ToIndentedString();
    }
}