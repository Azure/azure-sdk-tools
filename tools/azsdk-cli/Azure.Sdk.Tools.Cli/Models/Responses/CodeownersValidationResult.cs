using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class CodeownersValidationResult : Response
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

    public override string ToString()
    {
        var result = new List<string>
            {
                $"Username: {Username}",
                $"IsValid: {IsValidCodeOwner}",
                $"HasWritePermission: {HasWritePermission}",
                $"Status: {Status}",
                $"Message: {Message ?? "None"}"
            };

        if (Organizations?.Any() == true)
        {
            result.Add($"Organizations:");
            foreach (var org in Organizations)
            {
                result.Add($"  - {org.Key}: {org.Value}");
            }
        }

        return ToString(string.Join("\n", result));
    }
}
