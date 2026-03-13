// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Describes an expected tool call with optional input validation.
/// </summary>
public class ExpectedToolCall
{
    /// <summary>Gets the expected tool name (short name without MCP prefix).</summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets the expected input key-value pairs to validate, or null to skip input validation.
    /// Keys are parameter names; values are the expected values.
    /// String values use case-insensitive substring matching (to handle variable path prefixes).
    /// Numeric and boolean values use exact matching.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? ExpectedInputs { get; }

    /// <summary>
    /// Creates an expected tool call that only validates the tool was called (no input checks).
    /// </summary>
    public ExpectedToolCall(string toolName)
    {
        ToolName = toolName;
        ExpectedInputs = null;
    }

    /// <summary>
    /// Creates an expected tool call that validates both the tool name and its inputs.
    /// </summary>
    public ExpectedToolCall(string toolName, Dictionary<string, object?> expectedInputs)
    {
        ToolName = toolName;
        ExpectedInputs = expectedInputs;
    }

    public override string ToString() => ToolName;
}
