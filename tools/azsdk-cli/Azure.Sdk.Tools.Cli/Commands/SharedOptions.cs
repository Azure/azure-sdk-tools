using System.CommandLine;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tools.GitHub;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Tools.Pipeline;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.Example;
using Azure.Sdk.Tools.Cli.Tools.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.Verify;
using Azure.Sdk.Tools.Cli.Tools.Samples;

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
            typeof(SampleGeneratorTool),
            typeof(ReleasePlanTool),
            typeof(ReleaseReadinessTool),
            typeof(SdkBuildTool),
            typeof(SdkGenerationTool),
            typeof(MetadataUpdateTool),
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
            typeof(VerifySetupTool),
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
            Required = false,
        };

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
            root.Options.Add(ToolOption);

            var result = root.Parse(args);

            var raw = result.GetValue(ToolOption);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new string[] { };
            }

            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToLowerInvariant())
                .ToArray();
        }
    }
}
