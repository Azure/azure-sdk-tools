// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Represents a tool call captured during benchmark execution.
/// </summary>
public class ToolCallRecord
{
    /// <summary>Gets the tool name (may include MCP prefix).</summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the arguments passed to the tool, or null if unavailable.
    /// The raw object from the SDK hook (typically a JsonElement).
    /// </summary>
    public object? ToolArgs { get; init; }

    /// <summary>Gets the result returned by the tool, or null if unavailable.</summary>
    public object? ToolResult { get; init; }

    /// <summary>Gets the tool call duration in milliseconds, or null if unavailable.</summary>
    public double? DurationMs { get; init; }

    /// <summary>Gets the MCP server name extracted from the tool name prefix, or null.</summary>
    public string? McpServerName { get; init; }

    /// <summary>
    /// Gets the tool arguments as a string-keyed dictionary of JsonElements.
    /// Returns an empty dictionary if args are null or not a JSON object.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> GetArgsAsDictionary()
    {
        if (ToolArgs is not JsonElement { ValueKind: JsonValueKind.Object } jsonElement)
        {
            return new Dictionary<string, JsonElement>();
        }

        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in jsonElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }
        return dict;
    }

    public override string ToString() => ToolName;
}
