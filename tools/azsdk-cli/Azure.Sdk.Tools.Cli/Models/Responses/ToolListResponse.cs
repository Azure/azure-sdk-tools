// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;

namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public class ToolListResponse : CommandResponse
    {
        public IList<ToolInfo> Tools { get; set; } = new List<ToolInfo>();
        protected override string Format()
        {
            var sb = new StringBuilder();            
            if (Tools.Count == 0)
            {
                sb.AppendLine("No tools available.");
                return sb.ToString();
            }
            sb.AppendLine("Available Tools:");
            // Calculate max widths for each column
            int nameWidth = Tools.Max(t => t.McpToolName?.Length ?? 0);
            int cmdWidth = Tools.Max(t => t.CommandLine?.Length ?? 0);
            int descWidth = Tools.Max(t => t.Description?.Length ?? 0);
            nameWidth = Math.Max(nameWidth, "Name".Length);
            cmdWidth = Math.Max(cmdWidth, "Command".Length);
            descWidth = Math.Max(descWidth, "Description".Length);

            // Header
            sb.AppendLine($"| {"Name".PadRight(nameWidth)} | {"Command".PadRight(cmdWidth)} | {"Description".PadRight(descWidth)} |");
            sb.AppendLine($"|-{new string('-', nameWidth)}-|-{new string('-', cmdWidth)}-|-{new string('-', descWidth)}-|");

            // Rows
            foreach (var tool in Tools)
            {
                sb.AppendLine($"| {tool.McpToolName.PadRight(nameWidth)} | {tool.CommandLine.PadRight(cmdWidth)} | {tool.Description.PadRight(descWidth)} |");
            }
            return sb.ToString();
        }
    }
}
            


