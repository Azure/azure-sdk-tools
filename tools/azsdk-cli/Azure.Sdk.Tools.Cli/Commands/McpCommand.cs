// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public class McpCommand(string command, string description, string mcpToolName = "") : Command(command, description)
    {
        public string McpToolName { get; } = mcpToolName;
    }
}
