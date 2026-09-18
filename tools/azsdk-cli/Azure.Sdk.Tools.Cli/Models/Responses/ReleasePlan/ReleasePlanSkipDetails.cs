// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

/// <summary>
/// The first exclusion encountered for a plan in an overdue cleanup preview.
/// Evaluation failures are reported separately, not as successful exclusions.
/// </summary>
public class ReleasePlanSkipDetails(ReleasePlanWorkItem plan, string category, string reason)
{
    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; } = plan.WorkItemId;

    [JsonPropertyName("release_plan_id")]
    public int ReleasePlanId { get; } = plan.ReleasePlanId;

    [JsonPropertyName("title")]
    public string Title { get; } = plan.Title;

    [JsonPropertyName("target_month")]
    public string TargetMonth { get; } = plan.SDKReleaseMonth;

    [JsonPropertyName("release_plan_link")]
    public string ReleasePlanLink { get; } = plan.ReleasePlanLink;

    [JsonPropertyName("category")]
    public string Category { get; } = category;

    [JsonPropertyName("reason")]
    public string Reason { get; } = reason;
}