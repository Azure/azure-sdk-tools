// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Represents a tool call captured during benchmark execution.
/// </summary>
public class ToolCallRecord
{
    public required string ToolName { get; init; }
    public object? ToolArgs { get; init; }
    public object? ToolResult { get; init; }
}
