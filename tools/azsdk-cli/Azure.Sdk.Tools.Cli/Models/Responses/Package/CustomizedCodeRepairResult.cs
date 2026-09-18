// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairResult
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion => 1;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("terminalReason")]
    public string? TerminalReason { get; set; }

    [JsonPropertyName("repairKind")]
    public string RepairKind { get; set; } = "none";

    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; }

    [JsonPropertyName("timeoutMinutes")]
    public int TimeoutMinutes { get; set; }

    [JsonPropertyName("attemptsUsed")]
    public int AttemptsUsed => Attempts.Count;

    [JsonPropertyName("artifactsPath")]
    public string? ArtifactsPath { get; set; }

    [JsonPropertyName("input")]
    public CustomizedCodeRepairInput? Input { get; set; }

    [JsonPropertyName("finalState")]
    public CustomizedCodeRepairState? FinalState { get; set; }

    [JsonPropertyName("validation")]
    public CustomizedCodeRepairValidation Validation { get; set; } = new();

    [JsonPropertyName("stages")]
    public List<CustomizedCodeRepairStage> Stages { get; set; } = [];

    [JsonPropertyName("attempts")]
    public List<CustomizedCodeRepairAttempt> Attempts { get; set; } = [];
}
