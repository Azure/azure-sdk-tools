using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Contract
{
    /// <summary>
    /// This is the base class defining how an MCP enabled tool will interface with the server.
    ///
    /// This covers:
    ///     - route registration/disambiguation
    ///     - compilation trim avoidance for reflection-included MCP tools
    /// </summary>
    public abstract class MCPTool : MCPToolInterface
    {
        public MCPTool() {
            Command = GetCommand();
        }

        public Command Command;

        public abstract Command GetCommand();

        public abstract Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct);
    }
}
