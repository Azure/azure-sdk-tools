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
    /// Override execute mode to azsdk MCP server.
    /// When true, runs the azsdk MCP server in stdio mode; otherwise uses command line mode.
    /// </summary>
    public bool? RunAzsdkInMcpServer { get; init; }

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
}
