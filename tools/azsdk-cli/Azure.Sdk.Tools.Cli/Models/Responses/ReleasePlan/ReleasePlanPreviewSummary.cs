// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

/// <summary>
/// Scan counts with optional cleanup and notification outcomes.
/// </summary>
public class ReleasePlanPreviewSummary
{
    [JsonPropertyName("scanned")]
    public int Scanned { get; init; }

    [JsonPropertyName("eligible")]
    public int Eligible { get; init; }

    [JsonPropertyName("abandoned")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Abandoned { get; init; }

    [JsonPropertyName("delivered")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Delivered { get; init; }

    [JsonPropertyName("failed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Failed { get; init; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [JsonPropertyName("evaluation_errors")]
    public int EvaluationErrors { get; init; }

    [JsonPropertyName("skipped_by_reason")]
    public Dictionary<string, int> SkippedByReason { get; init; } = [];
}