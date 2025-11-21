// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Models
{
    public class ToolInfo
    {
        public ToolInfo(string mcpToolName, string commandLine, string description)
        {
            McpToolName = mcpToolName;
            Description = description;
            CommandLine = commandLine;
        }

        public string McpToolName { get; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommandLine { get; } = string.Empty;
    }
}
