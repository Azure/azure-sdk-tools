// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Models
{
    public class ToolInfo
    {
        public ToolInfo(string mcpToolName, string commandLine, string description, List<OptionInfo> options = null)
        {
            McpToolName = mcpToolName;
            Description = description;
            CommandLine = commandLine;
            Options = options ?? new List<OptionInfo>();
        }

        public string McpToolName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public List<OptionInfo> Options { get; set; } = [];
    }

    public class OptionInfo
    {
        public OptionInfo(string name, string description, string type, bool required)
        {
            Name = name;
            Description = description;
            Type = type;
            Required = required;
        }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool Required { get; set; }
    }
}
