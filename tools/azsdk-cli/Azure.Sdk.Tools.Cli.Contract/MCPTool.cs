using System.CommandLine;
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
        // we need some way to surface the command object here so we can use it to respond to the command invocation

        public abstract Command GetCommand { get; set; }


    }
}
