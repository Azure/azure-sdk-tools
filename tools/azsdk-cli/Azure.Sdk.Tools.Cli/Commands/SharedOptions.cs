using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Enumeration;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tools.GitHub;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Tools.Pipeline;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.Example;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedOptions
    {
        public static readonly List<Type> ToolsList = [
            typeof(PackageCheckTool),
            typeof(CleanupTool),
            typeof(CodeownersTools),
            typeof(GitHubLabelsTool),
            typeof(LogAnalysisTool),
            typeof(PipelineTool),
            typeof(PipelineAnalysisTool),
            typeof(PipelineTestsTool),
            typeof(QuokkaTool),
            typeof(ReadMeGeneratorTool),
            typeof(ReleasePlanTool),
            typeof(ReleaseReadinessTool),
            typeof(SdkBuildTool),
            typeof(SdkGenerationTool),
            typeof(SdkReleaseTool),
            typeof(SpecCommonTools),
            typeof(PullRequestTools),
            typeof(SpecWorkflowTool),
            typeof(SpecValidationTools),
            typeof(TestAnalysisTool),
            typeof(TypeSpecConvertTool),
            typeof(TypeSpecInitTool),
            typeof(TspClientUpdateTool),
            typeof(TypeSpecPublicRepoValidationTool),
            typeof(TestTool),
#if DEBUG
            // only add these tools in debug mode
            typeof(ExampleTool),
            typeof(HelloWorldTool),
#endif
        ];

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

        public static Option<string> PackagePath = new(["--package-path", "-p"], () => Environment.CurrentDirectory, "Path to the package directory to check. Defaults to the current working directory")
        {
            IsRequired = false
        };

        public static (string outputFormat, bool debug) GetGlobalOptionValues(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            root.AddGlobalOption(Format);
            root.AddGlobalOption(Debug);

            var parser = new Parser(root);
            var result = parser.Parse(args);

            var outputFormat = result.GetValueForOption(Format)?.ToLowerInvariant() ?? "";
            var debug = result.GetValueForOption(Debug);
            return (outputFormat, debug);
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
