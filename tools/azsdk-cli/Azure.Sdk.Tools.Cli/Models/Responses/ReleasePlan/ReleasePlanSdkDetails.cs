// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

public sealed class ReleasePlanSdkDetails(SDKInfo sdk)
{
    [JsonPropertyName("Language")]
    public string Language { get; } = sdk.Language;

    [JsonPropertyName("PackageName")]
    public string PackageName { get; } = sdk.PackageName;

    [JsonPropertyName("GenerationPipelineUrl")]
    public string GenerationPipelineUrl { get; } = sdk.GenerationPipelineUrl;

    [JsonPropertyName("SdkPullRequestUrl")]
    public string SdkPullRequestUrl { get; } = sdk.SdkPullRequestUrl;

    [JsonPropertyName("GenerationStatus")]
    public string GenerationStatus { get; } = sdk.GenerationStatus;

    [JsonPropertyName("ReleaseStatus")]
    public string ReleaseStatus { get; } = sdk.ReleaseStatus;

    [JsonPropertyName("PullRequestStatus")]
    public string PullRequestStatus { get; } = sdk.PullRequestStatus;

    [JsonPropertyName("ReleaseExclusionStatus")]
    public string ReleaseExclusionStatus { get; } = sdk.ReleaseExclusionStatus;
}