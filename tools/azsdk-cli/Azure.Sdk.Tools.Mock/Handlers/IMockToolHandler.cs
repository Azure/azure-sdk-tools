// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers;

/// <summary>
/// Interface for custom mock tool handlers. Implement this to provide
/// tool-specific mock responses instead of the default success response.
/// </summary>
public interface IMockToolHandler
{
    /// <summary>
    /// The MCP tool name this handler responds to (e.g., "azsdk_hello_world").
    /// Must match the [McpServerTool(Name = "...")] value from the real tool.
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Produce a mock response for the given tool invocation arguments.
    /// </summary>
    /// <param name="arguments">The arguments passed by the MCP client, or null if none.</param>
    /// <returns>A <see cref="CommandResponse"/> that will be JSON-serialized back to the caller.</returns>
    CommandResponse Handle(Dictionary<string, object?>? arguments);
}
