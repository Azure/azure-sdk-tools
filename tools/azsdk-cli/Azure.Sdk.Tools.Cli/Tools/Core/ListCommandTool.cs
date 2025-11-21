// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Core
{
    public class ListCommandTool(IServiceProvider serviceProvider) : MCPTool
    {
#pragma warning disable CS1998
        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            return new ToolListResponse
            {
                Tools = GetToolNameAndDescription(parseResult)
            };
        }
#pragma warning restore CS1998

        protected override Command GetCommand()
        {
            return new McpCommand("list", "List all available tools");
        }

        private static void ProcessCommandTree(Command command, string parent, List<ToolInfo> commandInfoList, IEnumerable<McpServerTool> registeredToolInstances)
        {
            // Process all subcommands
            foreach (var subcommand in command.Subcommands)
            {
                var newParent = string.IsNullOrEmpty(parent) ? command.Name : $"{parent} {command.Name}";
                ProcessCommandTree(subcommand, newParent, commandInfoList, registeredToolInstances);
            }

            // If current command is leaf command, add to list
            if (!command.Subcommands.Any())
            {
                string mcpToolName = "", description = command.Description ?? "";
                if (command is McpCommand mcpCommand)
                {
                    mcpToolName = mcpCommand.McpToolName;
                    // Validate that the MCP tool exists in registered tools
                    var registeredMcpTool = registeredToolInstances.FirstOrDefault(t => t.ProtocolTool.Name == mcpToolName);
                    if (!string.IsNullOrEmpty(mcpToolName) && registeredMcpTool == null)
                    {
                        throw new InvalidOperationException($"MCP Tool '{mcpToolName}' is not found in registered tools for command '{command.Name}'");
                    }
                    description = registeredMcpTool?.ProtocolTool?.Description ?? "";
                }
                commandInfoList.Add(new ToolInfo(mcpToolName, $"{parent} {command.Name}", description));
            }
        }

        private List<ToolInfo> GetToolNameAndDescription(ParseResult parseResult)
        {
            var tools = new List<ToolInfo>();
            var toolInstances = serviceProvider.GetServices<McpServerTool>();
            // Get all CLI commands from command root and its MCP tool name
            ProcessCommandTree(parseResult.RootCommandResult.Command, "", tools, toolInstances);

            // Find any MCP tool that is not represented in the command tree
            foreach (var mcpTool in toolInstances.Select(t=> t.ProtocolTool))
            {
                if (!tools.Any(t=> t.McpToolName == mcpTool.Name))
                {
                    // Add new tool that does not have CLI command representation
                    tools.Add(new ToolInfo(mcpTool.Name, "", mcpTool.Description ?? ""));
                }                
            }
            return tools.OrderBy(t => string.IsNullOrEmpty(t.McpToolName)).ThenBy(t => t.McpToolName).ToList();
        }
    }
}
