using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Azure.SDK.Tools.MCP.Contract;
using ModelContextProtocol.Server;

namespace Azure.SDK.Tools.MCP.Hub.Tools
{

    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorld : MCPHubTool
    {
        [McpServerTool, Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";
    }
}
