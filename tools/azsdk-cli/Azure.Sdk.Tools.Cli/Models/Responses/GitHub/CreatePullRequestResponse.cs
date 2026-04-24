// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Models.Responses.GitHub;

public class CreatePullRequestResponse : CommandResponse
{
    [JsonPropertyName("pull_request_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PullRequestUrl { get; set; }

    [JsonPropertyName("messages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Messages { get; set; }

    protected override string Format()
    {
        var output = new List<string>();

        if (!string.IsNullOrWhiteSpace(PullRequestUrl))
        {
            output.Add($"Pull request URL: {PullRequestUrl}");
        }

        if (Messages != null && Messages.Count > 0)
        {
            output.AddRange(Messages);
        }

        return string.Join(Environment.NewLine, output);
    }
}
