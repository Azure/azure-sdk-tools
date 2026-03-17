// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Configuration for executing a benchmark scenario.
/// </summary>
public class ExecutionConfig
{
    /// <summary>Working directory for the agent (repo path).</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>The prompt to send to the agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Maximum time to wait for the agent to complete.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Path to azsdk MCP server executable (optional override).</summary>
    public string? AzsdkMcpPath { get; init; }

    /// <summary>The model to use for the agent session.</summary>
    public string Model { get; init; } = BenchmarkDefaults.DefaultModel;

    /// <summary>
    /// Optional callback for activity updates during execution.
    /// Called with activity description (e.g., "Calling tool: view").
    /// </summary>
    public Action<string>? OnActivity { get; init; }

    /// <summary>
    /// Show agent activity during execution.
    /// </summary>
    public bool Verbose { get; init; }
}
