using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Enumeration;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Tools.HelloWorldTool;
using Azure.Sdk.Tools.Cli.Tools.HostServer;
using Azure.Sdk.Tools.Cli.Tools.ReleaseReadiness;
using Azure.Sdk.Tools.Cli.Tools.PromptValidationTool;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedOptions
    {
        public static readonly List<Type> ToolsList = new List<Type>(){
            typeof(AnalyzePipelinesTool),
            typeof(PipelineDetailsTool),
            typeof(CleanupTool),
            typeof(LogAnalysisTool),
            typeof(HostServerTool),
            typeof(ReleasePlanTool),
            typeof(SpecCommonTools),
            typeof(SpecPullRequestTools),
            typeof(SpecWorkflowTool),
            typeof(SpecValidationTools),
            typeof(ReleaseReadinessTool),
            typeof(PromptValidationTool),
            typeof(DownloadPromptsTool),
            #if DEBUG
            // only add this tool in debug mode
            typeof(HelloWorldTool),
            #endif
        };

        public static Option<string> ToolOption = new("--tools")
        {
            Description = "If provided, the tools server will only respond to CLI or MCP server requests for tools named the same as provided in this option. Glob matching is honored.",
            IsRequired = false,
        };

        public static Option<string> Format = new(["--output", "-o"], () => "plain")
        {
            Description = "The format of the output. Supported formats are: plain, json",
            IsRequired = false,
        };

        public static Option<bool> Debug = new(["--debug"], () => false)
        {
            Description = "Enable debug logging",
            IsRequired = false,
        };

        public static (string, bool) GetGlobalOptionValues(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            root.AddGlobalOption(Format);
            root.AddGlobalOption(Debug);

            var parser = new Parser(root);
            var result = parser.Parse(args);

            var raw = result.GetValueForOption(Format)?.ToLowerInvariant() ?? "";
            var debug = result.GetValueForOption(Debug);
            return (raw, debug);
        }

        public static string[] GetToolsFromArgs(string[] args)
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
            {
                return new string[] { };
            }

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .ToArray();
        }

        public static List<Type> GetFilteredToolTypes(string[] args)
        {
            var toolMatchList = SharedOptions.GetToolsFromArgs(args);

            if (toolMatchList.Length > 0)
            {
                return ToolsList
                             .Where(t => toolMatchList.Any(x => FileSystemName.MatchesSimpleExpression(x, t.Name) || t.Name.StartsWith("HostServer")))
                             .ToList();
            }

            return ToolsList;
        }
    }
}
