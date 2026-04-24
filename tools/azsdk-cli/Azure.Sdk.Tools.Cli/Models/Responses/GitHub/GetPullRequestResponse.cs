// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Models.Responses.GitHub;

public class GetPullRequestResponse : CommandResponse
{
    [JsonPropertyName("pull_request_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PullRequestUrl { get; set; }

    [JsonPropertyName("pull_request")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PullRequestDetails? PullRequest { get; set; }

    protected override string Format()
    {
        var output = new List<string>();

        if (!string.IsNullOrWhiteSpace(PullRequestUrl))
        {
            output.Add($"Pull request URL: {PullRequestUrl}");
        }

        if (PullRequest != null)
        {
            output.Add(JsonSerializer.Serialize(PullRequest, new JsonSerializerOptions { WriteIndented = true }));
        }

        return string.Join(Environment.NewLine, output);
    }
}
