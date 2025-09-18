using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class ServiceCodeownersResult : CommandResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("code_owners")]
    public List<CodeownersValidationResult> CodeOwners { get; set; } = new();

    public override string ToString()
    {
        var lines = new List<string>
        {
            $"Message: {Message}"
        };

        if (CodeOwners != null && CodeOwners.Count > 0)
        {
            lines.Add("Code Owners:");
            foreach (var owner in CodeOwners)
            {
                lines.Add("  - " + owner?.ToString());
            }
        }

        return ToString(string.Join(Environment.NewLine, lines));
    }
}
