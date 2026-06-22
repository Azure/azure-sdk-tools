// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers;

/// <summary>
/// Registry that auto-discovers <see cref="IMockToolHandler"/> implementations
/// and provides a lookup by tool name. Returns null for tools without a custom handler.
/// </summary>
public class MockToolFactory
{
    private readonly Dictionary<string, IMockToolHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public MockToolFactory()
    {
        DiscoverHandlers();
    }

    /// <summary>
    /// Gets the custom handler for the given tool name, or null if none exists.
    /// </summary>
    public IMockToolHandler? GetHandler(string toolName)
    {
        _handlers.TryGetValue(toolName, out var handler);
        return handler;
    }

    /// <summary>
    /// Produces the default mock response used when no custom handler is registered.
    /// Matches the shape of <c>DefaultCommandResponse</c> from the real CLI.
    /// </summary>
    public static CommandResponse GetDefaultResponse()
    {
        return new DefaultCommandResponse { Message = "Success" };
    }

    private void DiscoverHandlers()
    {
        var handlerType = typeof(IMockToolHandler);
        var implementations = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && handlerType.IsAssignableFrom(t));

        foreach (var type in implementations)
        {
            if (Activator.CreateInstance(type) is IMockToolHandler handler)
            {
                _handlers[handler.ToolName] = handler;
            }
        }
    }
}
