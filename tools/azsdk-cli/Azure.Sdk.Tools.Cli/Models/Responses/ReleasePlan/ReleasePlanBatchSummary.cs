// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

/// <summary>
/// Counts for a release-plan scan, cleanup, or email batch.
/// </summary>
public class ReleasePlanBatchSummary
{
    [JsonPropertyName("scanned")]
    public int Scanned { get; init; }

    [JsonPropertyName("eligible")]
    public int Eligible { get; init; }

    // Plans marked Abandoned by actual cleanup; omitted in previews and email-only batches.
    [JsonPropertyName("abandoned_plan_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbandonedPlanCount { get; init; }

    // Accepted email requests, not inbox delivery; omitted when no email batch was requested.
    [JsonPropertyName("accepted_email_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AcceptedEmailCount { get; init; }

    // Failed cleanup or email items; previews use EvaluationErrors instead.
    [JsonPropertyName("failed_item_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FailedItemCount { get; init; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [JsonPropertyName("evaluation_errors")]
    public int EvaluationErrors { get; init; }

    [JsonPropertyName("skipped_by_reason")]
    public Dictionary<string, int> SkippedByReason { get; init; } = [];
}