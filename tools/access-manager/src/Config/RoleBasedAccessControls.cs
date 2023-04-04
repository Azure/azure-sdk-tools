using System.Text;
using System.Text.Json.Serialization;

public class RoleBasedAccessControl
{
    [JsonRequired, JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonRequired, JsonPropertyName("scope")]
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