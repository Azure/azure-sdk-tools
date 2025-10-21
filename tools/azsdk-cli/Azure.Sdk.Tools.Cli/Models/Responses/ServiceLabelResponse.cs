using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class ServiceLabelResponse : CommandResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("pull_request_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PullRequestUrl { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    protected override string Format()
    {
        var output = $"Status: {Status}" + Environment.NewLine +
                     $"Label: {Label}";

        if (!string.IsNullOrEmpty(PullRequestUrl))
        {
            output += Environment.NewLine + $"Pull Request URL: {PullRequestUrl}";
        }

        return output;
    }
}
