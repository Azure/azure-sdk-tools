using System.Text;
using System.Text.Json.Serialization;

public class GithubRepositorySecretsConfig : BaseConfig
{
    [JsonRequired, JsonPropertyName("repositories")]
    public List<string> Repositories { get; set; } = new List<string>();

    [JsonRequired, JsonPropertyName("secrets")]
    public Dictionary<string, string> Secrets { get; set; } = new Dictionary<string, string>();

    public string ToIndentedString(int indentLevel = 0)
    {
        var indent = "";
        foreach (var lvl in Enumerable.Range(0, indentLevel))
        {
            indent += "    ";
        }

        var sb = new StringBuilder();
        sb.AppendLine(indent + $"Repositories: {string.Join(", ", Repositories)}");
        sb.AppendLine(indent + "Secrets:");
        foreach (var secret in Secrets ?? new Dictionary<string, string>())
        {
            sb.AppendLine(indent + indent + $"'{secret.Key}': '{secret.Value}'");
        }
        return sb.ToString();
    }

    public override string ToString()
    {
        return ToIndentedString();
    }
}