// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Mock.Handlers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Mock;

/// <summary>
/// Registers mock MCP tools by reflecting over <see cref="SharedOptions.ToolsList"/>,
/// preserving each tool's name, description, and parameter schema, but replacing the
/// real implementation with <see cref="MockToolFactory"/> dispatch.
/// </summary>
public static class MockToolRegistrations
{
    public static void RegisterMockMcpTools(IServiceCollection services)
    {
        foreach (var toolType in SharedOptions.ToolsList)
        {
            if (toolType is null) continue;

            var toolMethods = toolType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

            foreach (var toolMethod in toolMethods)
            {
                services.AddSingleton<McpServerTool>(sp =>
                {
                    var options = new McpServerToolCreateOptions { Services = sp };

                    // Create the real McpServerTool to capture its metadata (name, description, schema).
                    // For instance methods, we use an uninitialized dummy — the method is never actually invoked.
                    var innerTool = toolMethod.IsStatic
                        ? McpServerTool.Create(toolMethod, options: options)
                        : McpServerTool.Create(toolMethod, _ => RuntimeHelpers.GetUninitializedObject(toolType), options);

                    return new MockMcpServerTool(innerTool, sp.GetRequiredService<MockToolFactory>());
                });
            }
        }
    }
}

/// <summary>
/// An MCP tool wrapper that intercepts invocations and routes them through
/// <see cref="MockToolFactory"/> instead of executing the real tool logic.
/// Preserves the original tool's protocol metadata (name, description, input schema).
/// </summary>
internal class MockMcpServerTool(McpServerTool innerTool, MockToolFactory factory) : DelegatingMcpServerTool(innerTool)
{
    public override IReadOnlyList<object> Metadata => innerTool.Metadata;

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken ct = default)
    {
        var toolName = request?.Params?.Name ?? ProtocolTool.Name;
        var arguments = request?.Params?.Arguments?.ToDictionary(
            kvp => kvp.Key, kvp => (object?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        var handler = factory.GetHandler(toolName);
        var response = handler != null
            ? handler.Handle(arguments)
            : MockToolFactory.GetDefaultResponse();

        return ValueTask.FromResult(new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(response, response.GetType()) }]
        });
    }
}
