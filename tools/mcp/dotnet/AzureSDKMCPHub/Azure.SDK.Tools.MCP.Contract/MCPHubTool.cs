using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Azure.SDK.Tools.MCP.Contract
{
    /// <summary>
    /// This is the base class defining how an MCP enabled tool will interface with the server.
    /// 
    /// This covers: 
    ///     - route registration/disambiguation
    ///     - compilation trim avoidance for reflection-included MCP tools
    /// </summary>
    public abstract class MCPHubTool : MCToolInterface
    {
        // does McpServerToolType take care of everything here? do we even need anything?
    }
}
