// This file demonstrates the naming convention analyzers in action
// To see the analyzers work, remove the #pragma warning disable lines below

#pragma warning disable MCP003 // CLI command names must follow kebab-case convention
#pragma warning disable MCP004 // McpServerTool attribute must specify a Name property  
#pragma warning disable MCP005 // McpServerTool Name must follow snake_case convention

using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Examples
{
    /// <summary>
    /// This class demonstrates the naming convention analyzers.
    /// Remove the #pragma warning disable statements to see the analyzers in action.
    /// </summary>
    public class NamingConventionDemo
    {
        public void CreateExampleCommands()
        {
            // When analyzers are enabled, this would trigger MCP003 error due to camelCase
            var invalidCommand = new Command("myNewCommand", "Demonstrates analyzer detection");
            
            // This is correct - follows kebab-case
            var validCommand = new Command("my-new-command", "Correct kebab-case naming");
        }

        // When analyzers are enabled, this would trigger MCP004 error (missing Name)
        [McpServerTool, Description("Demonstrates missing Name property")]
        public void DemoMethod1() { }

        // When analyzers are enabled, this would trigger MCP005 error (kebab-case instead of snake_case)
        [McpServerTool(Name = "demo-method"), Description("Demonstrates wrong naming convention")]
        public void DemoMethod2() { }

        // This is the correct way - follows snake_case naming
        [McpServerTool(Name = "demo_method"), Description("Correct snake_case naming")]
        public void DemoMethod3() { }
    }
}