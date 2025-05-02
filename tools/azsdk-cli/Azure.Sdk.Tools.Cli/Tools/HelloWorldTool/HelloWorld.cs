using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.HelloWorldTool
{

    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorld : MCPTool
    {
        [McpServerTool, Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";


    }
}
