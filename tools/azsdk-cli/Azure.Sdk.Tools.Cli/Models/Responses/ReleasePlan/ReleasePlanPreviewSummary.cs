// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

public class ReleasePlanPreviewSummary
{
    [JsonPropertyName("scanned")]
    public int Scanned { get; init; }

    [JsonPropertyName("eligible")]
    public int Eligible { get; init; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [JsonPropertyName("evaluation_errors")]
    public int EvaluationErrors { get; init; }

    [JsonPropertyName("skipped_by_reason")]
    public Dictionary<string, int> SkippedByReason { get; init; } = [];
}