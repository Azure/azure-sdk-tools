// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public class McpCommand: Command
    {
        public string McpToolName { get; }
        public McpCommand(string command, string description, string mcpToolName = "") : base(command, description)
        {
            McpToolName = mcpToolName;
        }
    }
}
