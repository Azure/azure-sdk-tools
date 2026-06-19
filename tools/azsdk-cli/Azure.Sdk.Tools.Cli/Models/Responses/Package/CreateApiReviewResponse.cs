// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.ApiReview;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class CreateApiReviewResponse : PackageResponseBase
{
    [JsonPropertyName("base")]
    public string? Base { get; set; }

    [JsonPropertyName("target")]
    public ApiReviewTarget? Target { get; set; }

    [JsonPropertyName("base_branch")]
    public string? BaseBranch { get; set; }

    [JsonPropertyName("head_branch")]
    public string? HeadBranch { get; set; }

    [JsonPropertyName("pull_request_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PullRequestUrl { get; set; }

    [JsonPropertyName("artifacts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ApiReviewArtifact>? Artifacts { get; set; }

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
        if (!string.IsNullOrWhiteSpace(BaseBranch))
        {
            output.Add($"Base branch: {BaseBranch}");
        }
        if (!string.IsNullOrWhiteSpace(HeadBranch))
        {
            output.Add($"Head branch: {HeadBranch}");
        }
        if (Artifacts?.Count > 0)
        {
            output.Add("Artifacts:");
            output.AddRange(Artifacts.Select(artifact => artifact.ReviewPath));
        }
        if (Messages?.Count > 0)
        {
            output.AddRange(Messages);
        }
        return string.Join(Environment.NewLine, output);
    }
}
