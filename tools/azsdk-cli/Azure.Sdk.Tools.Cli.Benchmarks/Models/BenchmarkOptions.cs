// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Options for running a benchmark.
/// </summary>
public class BenchmarkOptions
{
    /// <summary>
    /// Override path to azsdk MCP server.
    /// </summary>
    public string? AzsdkMcpPath { get; init; }

    /// <summary>
    /// Cleanup policy after run.
    /// </summary>
    public CleanupPolicy CleanupPolicy { get; init; } = CleanupPolicy.OnSuccess;

    /// <summary>
    /// Override model to use (for future use).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Show agent activity during execution.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Ref overrides keyed by "Owner/Name". When a scenario's repo matches,
    /// the ref is replaced before workspace preparation. Null values mean no override.
    /// </summary>
    public Dictionary<string, string?>? RefOverrides { get; init; }
}
