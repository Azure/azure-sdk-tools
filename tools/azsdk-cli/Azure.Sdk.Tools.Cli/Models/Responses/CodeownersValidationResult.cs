using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class CodeownersValidationResult : CommandResponse
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("is_valid_code_owner")]
    public bool IsValidCodeOwner { get; set; }

    [JsonPropertyName("has_write_permission")]
    public bool HasWritePermission { get; set; }

    [JsonPropertyName("organizations")]
    public Dictionary<string, bool> Organizations { get; set; } = new();

    protected override string Format()
    {
        var result = new StringBuilder();
        result.AppendLine($"Username: {Username}");
        result.AppendLine($"IsValid: {IsValidCodeOwner}");
        result.AppendLine($"HasWritePermission: {HasWritePermission}");
        result.AppendLine($"Status: {Status}");
        result.AppendLine($"Message: {Message ?? "None"}");

        if (Organizations?.Any() == true)
        {
            result.AppendLine("Organizations:");
            foreach (var org in Organizations)
            {
                result.AppendLine($"  - {org.Key}: {org.Value}");
            }
        }

        return result.ToString();
    }
}
