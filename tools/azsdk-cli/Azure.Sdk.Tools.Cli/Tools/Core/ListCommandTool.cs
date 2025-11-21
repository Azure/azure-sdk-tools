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

        private static void ProcessCommandTree(Command command, string parent, List<ToolInfo> commandInfoList)
        {
            // Process all subcommands
            foreach (var subcommand in command.Subcommands)
            {
                var newParent = string.IsNullOrEmpty(parent) ? command.Name : $"{parent} {command.Name}";
                ProcessCommandTree(subcommand, newParent, commandInfoList);
            }

            // If current command is leaf command, add to list
            if (!command.Subcommands.Any())
            {
                string mcpToolName = "";
                if (command is McpCommand mcpCommand)
                {
                    mcpToolName = mcpCommand.McpToolName;
                }
                commandInfoList.Add(new ToolInfo(mcpToolName, $"{parent} {command.Name}", command.Description ?? ""));
            }
        }

        private List<ToolInfo> GetToolNameAndDescription(ParseResult parseResult)
        {
            var tools = new List<ToolInfo>();
            // Get all CLI commands from command root and its MCP tool name
            ProcessCommandTree(parseResult.RootCommandResult.Command, "", tools);

            // Find any MCP tool that is not represented in the command tree
            // Also get MCP tool description and update in the list
            var toolInstances = serviceProvider.GetServices<McpServerTool>();
            foreach (var toolInstance in toolInstances)
            {
                var mcpTool = toolInstance.ProtocolTool;

                // Update the description if tool already exists in the list
                var mcpToolInfo = tools.FirstOrDefault(t => t.McpToolName == mcpTool.Name);
                if (mcpToolInfo != null)
                {
                    mcpToolInfo.Description = mcpTool.Description ?? "";
                    continue;
                }

                // Add new tool that does not have CLI command representation
                tools.Add(new ToolInfo(mcpTool.Name, "", mcpTool.Description ?? ""));
            }

            return tools.OrderBy(t => string.IsNullOrEmpty(t.McpToolName)).ThenBy(t => t.McpToolName).ToList();
        }
    }
}
