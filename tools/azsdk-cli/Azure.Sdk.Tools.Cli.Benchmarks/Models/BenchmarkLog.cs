// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Represents the benchmark execution log written to benchmark-log.json.
/// </summary>
public class BenchmarkLog
{
    public string Scenario { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string Duration { get; set; } = "";
    public bool Passed { get; set; }
    public string? Error { get; set; }
    public ValidationSummary? Validation { get; set; }
    public TokenUsage? TokenUsage { get; set; }
    public List<ToolCallRecord> ToolCalls { get; set; } = [];
    public List<object> Messages { get; set; } = [];
    public string? GitDiff { get; set; }
}
