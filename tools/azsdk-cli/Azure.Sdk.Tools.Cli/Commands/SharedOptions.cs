using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Contract;
using System.IO.Enumeration;
using System.Reflection;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class SharedOptions
    {
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

        public static string GetOutputFormat(string[] args)
        {
            var root = new RootCommand
            {
                TreatUnmatchedTokensAsErrors = false
            };
            root.AddOption(Format);

            var parser = new Parser(root);
            var result = parser.Parse(args);

            var raw = result.GetValueForOption(Format);
            return raw?.ToLowerInvariant();
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
            if (string.IsNullOrWhiteSpace(raw)) {
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
            List<Type> toolsList;

            if (toolMatchList.Length > 0)
            {
                toolsList = AppDomain.CurrentDomain
                             .GetAssemblies()
                             .SelectMany(a => SafeGetTypes(a))
                             .Where(t => !t.IsAbstract &&
                             typeof(MCPTool).IsAssignableFrom(t))
                             .Where(t => toolMatchList.Any(x => FileSystemName.MatchesSimpleExpression(x, t.Name) || t.Name.StartsWith("HostServer")))
                             .ToList();
            }
            else
            {
                // defaults to everything
                toolsList = AppDomain.CurrentDomain
                             .GetAssemblies()
                             .SelectMany(a => SafeGetTypes(a))
                             .Where(t => !t.IsAbstract &&
                             typeof(MCPTool).IsAssignableFrom(t))
                             .ToList();
            }

            return toolsList;
        }

        public static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types!.Where(t => t != null)!;
            }
        }
    }
}
