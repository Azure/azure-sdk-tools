// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairAttempt
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("hypothesis")]
    public string? Hypothesis { get; set; }

    [JsonPropertyName("sourceTreeBefore")]
    public string? SourceTreeBefore { get; set; }

    [JsonPropertyName("sourceTreeAfterPatch")]
    public string? SourceTreeAfterPatch { get; set; }

    [JsonPropertyName("sourceTreeAfterValidation")]
    public string? SourceTreeAfterValidation { get; set; }

    [JsonPropertyName("patchDiffPath")]
    public string? PatchDiffPath { get; set; }

    [JsonPropertyName("validationDiffPath")]
    public string? ValidationDiffPath { get; set; }

    [JsonPropertyName("diagnosticSummary")]
    public string? DiagnosticSummary { get; set; }

    [JsonPropertyName("reversedPatchCount")]
    public int ReversedPatchCount { get; set; }

    [JsonPropertyName("stageIds")]
    public List<string> StageIds { get; set; } = [];
}
