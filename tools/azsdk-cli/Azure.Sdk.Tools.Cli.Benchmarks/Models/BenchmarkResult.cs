// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Result of running a benchmark scenario.
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// Gets the name of the scenario that was executed.
    /// </summary>
    public required string ScenarioName { get; init; }

    /// <summary>
    /// Gets whether the benchmark passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets the error message if the benchmark failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the duration of the benchmark execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the git diff of changes made during the benchmark.
    /// </summary>
    public string? GitDiff { get; init; }

    /// <summary>
    /// Gets the list of tool calls made during execution.
    /// </summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];

    /// <summary>
    /// Gets the path to the workspace where the benchmark was executed.
    /// </summary>
    public string? WorkspacePath { get; init; }

    /// <summary>
    /// Gets whether the workspace was cleaned up after execution.
    /// </summary>
    public bool WorkspaceCleanedUp { get; init; }

    /// <summary>
    /// Gets the validation summary (null if no validators defined).
    /// </summary>
    public ValidationSummary? Validation { get; init; }
}
