// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.ApiReview;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiReviewTargetKind
{
    Tag,
    RemoteBranch,
    ForkBranch
}

public class ApiReviewTarget
{
    [JsonPropertyName("raw")]
    public required string Raw { get; set; }

    [JsonPropertyName("kind")]
    public ApiReviewTargetKind Kind { get; set; }

    [JsonPropertyName("remote")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remote { get; set; }

    [JsonPropertyName("owner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Owner { get; set; }

    [JsonPropertyName("branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Branch { get; set; }

    public string GitRef => Kind switch
    {
        ApiReviewTargetKind.RemoteBranch => $"{Remote}/{Branch}",
        ApiReviewTargetKind.ForkBranch => $"{Owner}/{Branch}",
        _ => Raw,
    };

    public static ApiReviewTarget Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw, nameof(raw));

        var value = raw.Trim();
        var colonIndex = value.IndexOf(':');
        if (colonIndex > 0 && colonIndex < value.Length - 1 && !value[..colonIndex].Contains('/'))
        {
            return new ApiReviewTarget
            {
                Raw = value,
                Kind = ApiReviewTargetKind.ForkBranch,
                Owner = value[..colonIndex],
                Branch = value[(colonIndex + 1)..]
            };
        }

        var slashIndex = value.IndexOf('/');
        if (slashIndex > 0 && slashIndex < value.Length - 1)
        {
            return new ApiReviewTarget
            {
                Raw = value,
                Kind = ApiReviewTargetKind.RemoteBranch,
                Remote = value[..slashIndex],
                Branch = value[(slashIndex + 1)..]
            };
        }

        return new ApiReviewTarget
        {
            Raw = value,
            Kind = ApiReviewTargetKind.Tag
        };
    }
}
