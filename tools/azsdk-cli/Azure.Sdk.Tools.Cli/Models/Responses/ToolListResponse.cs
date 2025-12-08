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

            static List<string> OptionsToLines(ToolInfo t)
            {
                if (t?.Options == null || t.Options.Count == 0) { return new List<string>(); }
                return t.Options.Select(o =>
                    $"Name: {o.Name} | Type: {o.Type} | Required: {(o.Required ? "Yes" : "No")}"
                ).ToList();
            }

            // Calculate max widths for each column
            int nameWidth = Tools.Max(t => t.McpToolName?.Length ?? 0);
            int cmdWidth = Tools.Max(t => t.CommandLine?.Length ?? 0);
            int descWidth = Tools.Max(t => t.Description?.Length ?? 0);

            int optsWidth = Math.Max(
                Tools.SelectMany(t => OptionsToLines(t))
                     .DefaultIfEmpty(string.Empty)
                     .Max(line => line?.Length ?? 0),
                "Options".Length);
            nameWidth = Math.Max(nameWidth, "Name".Length);
            cmdWidth = Math.Max(cmdWidth, "Command".Length);
            descWidth = Math.Max(descWidth, "Description".Length);

            const string sep = " | ";
            // --- Header ---
            sb.AppendLine(
                $"{("Name").PadRight(nameWidth)}{sep}" +
                $"{("Command").PadRight(cmdWidth)}{sep}" +
                $"{("Description").PadRight(descWidth)}{sep}" +
                $"{("Options").PadRight(optsWidth)}"
            );

            // --- Underline ---
            sb.AppendLine(
                $"{new string('-', nameWidth)}{sep}" +
                $"{new string('-', cmdWidth)}{sep}" +
                $"{new string('-', descWidth)}{sep}" +
                $"{new string('-', optsWidth)}"
            );

            // --- Rows ---
            foreach (var tool in Tools)
            {
                var optionLines = OptionsToLines(tool);

                // First line: print all columns
                string nameCell = (tool?.McpToolName ?? string.Empty).PadRight(nameWidth);
                string cmdCell = (tool?.CommandLine ?? string.Empty).PadRight(cmdWidth);
                string descCell = (tool?.Description ?? string.Empty).PadRight(descWidth);

                if (optionLines.Count == 0)
                {
                    sb.AppendLine($"{nameCell}{sep}{cmdCell}{sep}{descCell}{sep}{string.Empty.PadRight(optsWidth)}");
                    continue;
                }

                sb.AppendLine($"{nameCell}{sep}{cmdCell}{sep}{descCell}{sep}{optionLines[0].PadRight(optsWidth)}");

                // Continuation lines: only Options column, blanks elsewhere
                for (int i = 1; i < optionLines.Count; i++)
                {
                    sb.AppendLine(
                        $"{string.Empty.PadRight(nameWidth)}{sep}" +
                        $"{string.Empty.PadRight(cmdWidth)}{sep}" +
                        $"{string.Empty.PadRight(descWidth)}{sep}" +
                        $"{optionLines[i].PadRight(optsWidth)}"
                    );
                }

                    sb.AppendLine(
                        new string('-', nameWidth) + sep +
                        new string('-', cmdWidth) + sep + 
                        new string('-', descWidth)+ sep +
                        new string('-', optsWidth)
                    );
            }
            return sb.ToString();
        }
    }
}
