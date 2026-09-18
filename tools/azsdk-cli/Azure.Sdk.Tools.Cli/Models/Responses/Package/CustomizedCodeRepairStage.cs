// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairStage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("attempt")]
    public int Attempt { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "failed";

    [JsonPropertyName("sourceTreeBefore")]
    public string? SourceTreeBefore { get; set; }

    [JsonPropertyName("sourceTreeAfter")]
    public string? SourceTreeAfter { get; set; }

    [JsonPropertyName("elapsedMilliseconds")]
    public long ElapsedMilliseconds { get; set; }

    [JsonPropertyName("diagnosticsPath")]
    public string? DiagnosticsPath { get; set; }

    [JsonPropertyName("diagnosticSummary")]
    public string? DiagnosticSummary { get; set; }
}
