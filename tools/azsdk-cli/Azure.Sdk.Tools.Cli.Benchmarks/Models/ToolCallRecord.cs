// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Represents a single tool call made during benchmark execution,
/// capturing rich metadata for reporting.
/// </summary>
public class ToolCallRecord
{
    /// <summary>
    /// Gets the name of the tool that was called.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the serialized JSON of the tool's input arguments (null if not captured).
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>
    /// Gets the duration of the tool call in milliseconds (null if not measured).
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// Gets the name of the MCP server that provided this tool (null for built-in tools).
    /// </summary>
    public string? McpServerName { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the tool call started.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
