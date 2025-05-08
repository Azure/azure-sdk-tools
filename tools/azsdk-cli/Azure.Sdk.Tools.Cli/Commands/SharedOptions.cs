using System.CommandLine;
using System.CommandLine.Parsing;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedOptions
    {
        public static Option<string> ToolOption = new Option<string>("--tools")
        {
            Description = "If provided, the tools server will only respond to CLI or MCP server requests for tools named the same as provided in this option. Glob matching is honored.",
            IsRequired = false,
        };

        public static List<string> GetToolsFromArgs(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            root.AddOption(ToolOption);

            var parser = new Parser(root);
            var result = parser.Parse(args);

            var raw = result.GetValueForOption(ToolOption);
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .ToList();
        }
    }
}
