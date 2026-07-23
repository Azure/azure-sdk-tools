using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.AccessManagement;

public class FederatedIdentityCredentialsConfig : BaseConfig
{
    [JsonRequired, JsonPropertyName("audiences"), JsonPropertyOrder(0)]
    public List<string>? Audiences { get; set; }

    [JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyOrder(1)]
    public string? Description { get; set; }

    [JsonRequired, JsonPropertyName("issuer"), JsonPropertyOrder(2)]
    public string? Issuer { get; set; }

    [JsonRequired, JsonPropertyName("name"), JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonRequired, JsonPropertyName("subject"), JsonPropertyOrder(4)]
    public string? Subject { get; set; }

    public bool Matches(FederatedCredentialInfo info)
    {
        if (info.Audiences?.SequenceEqual(Audiences ?? Enumerable.Empty<string>()) == false)
        {
            return false;
        }
        return info.Name == Name &&
               info.Issuer?.TrimEnd('/') == Issuer?.TrimEnd('/') &&
               info.Subject == Subject;
    }

    public override int GetHashCode() => HashCode.Combine(Audiences, Description, Issuer, Name, Subject);

    public string ToIndentedString(int indentLevel = 0)
    {
        var indent = "";
        foreach (var lvl in Enumerable.Range(0, indentLevel))
        {
            indent += "    ";
        }

        var sb = new StringBuilder();
        sb.AppendLine(indent + $"Audiences: {string.Join(", ", Audiences!)}");
        sb.AppendLine(indent + $"Description: {Description}");
        sb.AppendLine(indent + $"Issuer: {Issuer}");
        sb.AppendLine(indent + $"Name: {Name}");
        sb.AppendLine(indent + $"Subject: {Subject}");

        return sb.ToString();
    }

    public override string ToString()
    {
        return ToIndentedString();
    }
}