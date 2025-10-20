using System;
using System.CommandLine;
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
            typeof(CodeownersTool),
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
            typeof(CleanupTool),
            typeof(ExampleTool),
            typeof(HelloWorldTool),
#endif
        ];

        public static Option<string> Format = new("--output", "-o")
        {
            Description = "The format of the output. Supported formats are: plain, json",
            Required = false,
            Recursive = true,
            DefaultValueFactory = _ => "plain",
        };

        public static Option<bool> Debug = new("--debug")
        {
            Description = "Enable debug logging",
            Required = false,
            Recursive = true,
            DefaultValueFactory = _ => false,
        };

        public static Option<string> PackagePath = new("--package-path", "-p")
        {
            Description = "Path to the package directory to check. Defaults to the current working directory",
            Required = false,
            DefaultValueFactory = _ => Environment.CurrentDirectory,
        };

        public static (string outputFormat, bool debug) GetGlobalOptionValues(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            root.Options.Add(Format);
            root.Options.Add(Debug);

            var result = root.Parse(args);

            // Note: When --help is present, GetValue returns null instead of calling DefaultValueFactory
            // because the command isn't actually being invoked. The ?? "plain" fallback handles this case.
            var outputFormat = result.GetValue(Format)?.ToLowerInvariant() ?? "plain";
            var debug = result.GetValue(Debug);
            return (outputFormat, debug);
        }

        public static string[] GetToolsFromArgs(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            Option<string> toolOption = new("--tools");
            root.Options.Add(toolOption);

            var result = root.Parse(args);

            var raw = result.GetValue(toolOption);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .ToArray();
        }

        public static List<Type> GetFilteredToolTypes(string[] args)
        {
            var toolMatchList = GetToolsFromArgs(args);

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
