using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.AccessManagement;

public class FederatedIdentityCredentialsConfig : BaseConfig, IEquatable<FederatedIdentityCredential>
{
    [JsonRequired, JsonPropertyName("audiences"), JsonPropertyOrder(0)]
    public List<string>? Audiences { get; set; }

    [JsonRequired, JsonPropertyName("description"), JsonPropertyOrder(1)]
    public string? Description { get; set; }

    [JsonRequired, JsonPropertyName("issuer"), JsonPropertyOrder(2)]
    public string? Issuer { get; set; }

    [JsonRequired, JsonPropertyName("name"), JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonRequired, JsonPropertyName("subject"), JsonPropertyOrder(4)]
    public string? Subject { get; set; }

    public static implicit operator FederatedIdentityCredential(FederatedIdentityCredentialsConfig config)
    {
        return new FederatedIdentityCredential
        {
            Name = config.Name,
            Description = config.Description,
            Issuer = config.Issuer,
            Subject = config.Subject,
            Audiences = config.Audiences
        };
    }

    public static explicit operator FederatedIdentityCredentialsConfig(FederatedIdentityCredential cred)
    {
        return new FederatedIdentityCredentialsConfig
        {
            Name = cred.Name,
            Description = cred.Description,
            Issuer = cred.Issuer,
            Subject = cred.Subject,
            Audiences = cred.Audiences
        };
    }

    public bool Equals(FederatedIdentityCredential? cred)
    {
        if (cred?.Audiences?.SequenceEqual(Audiences ?? Enumerable.Empty<string>()) == false)
        {
            return false;
        }
        return cred?.Name == Name &&
               cred?.Description == Description &&
               cred?.Issuer == Issuer &&
               cred?.Subject == Subject;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null || !(obj is FederatedIdentityCredential))
        {
            return false;
        }

        return Equals((FederatedIdentityCredential)obj);
    }

    public static bool operator ==(FederatedIdentityCredentialsConfig? cfg, object? obj)
    {
        if (cfg is null && obj is null)
        {
            return true;
        }

        return cfg?.Equals(obj) ?? false;
    }

    public static bool operator !=(FederatedIdentityCredentialsConfig? cfg, object? obj) => !(cfg == obj);

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