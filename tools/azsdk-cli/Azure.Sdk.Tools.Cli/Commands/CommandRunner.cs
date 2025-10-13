using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Commands.HostServer;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class CommandRunner
    {
        /// <summary>
        /// Creates the primary parsing entry point for the application and runs the command
        /// </summary>
        /// <returns>Command exit code</returns>
        public static async Task<int> BuildAndRun(
            string[] args,
            IServiceProvider serviceProvider,
            bool debug = false
        )
        {
            var rootCommand = new RootCommand("azsdk cli - A Model Context Protocol (MCP) server that facilitates tasks for anyone working with the Azure SDK team.");
            rootCommand.AddOption(SharedOptions.ToolOption);

            rootCommand.AddGlobalOption(SharedOptions.Debug);

            SharedOptions.Format.AddValidator(result =>
            {
                var value = result.GetValueForOption(SharedOptions.Format);
                if (value != "plain" && value != "json" && value != "hidden")
                {
                    // hidden is used for tests, don't include in help text
                    result.ErrorMessage = $"Invalid output format '{value}'. Supported formats are: plain, json";
                }
            });
            rootCommand.AddGlobalOption(SharedOptions.Format);

            // Create the MCP server command at the root as the MCP SDK has injected
            // singletons within WithStdioServerTransport() and will not run
            // within the DI scope we create for CLI commands.
            var hostServer = ActivatorUtilities.CreateInstance<HostServerCommand>(serviceProvider);
            rootCommand.AddCommand(hostServer.GetCommand());

            var toolTypes = SharedOptions
                                .GetFilteredToolTypes(args)
                                .Where(t => t.Name != nameof(HostServerCommand));

            // Many services are injected as scoped so they will be unique
            // per request when running in MCP server mode. Create a base scope
            // here so we can resolve those services in CLI mode as well.
            using var scope = serviceProvider.CreateAsyncScope();
            var scopedProvider = scope.ServiceProvider;
            var toolInstances = toolTypes
                .Select(t =>
                {
                    var _tool = (MCPToolBase)ActivatorUtilities.CreateInstance(scopedProvider, t);
                    _tool.Initialize(
                        scopedProvider.GetRequiredService<IOutputHelper>(),
                        scopedProvider.GetRequiredService<ITelemetryService>(),
                        debug);
                    return _tool;
                })
                .ToList();

            PopulateToolHierarchy(rootCommand, toolInstances);

            var parsedCommands = new CommandLineBuilder(rootCommand)
                   .UseDefaults()            // adds help, version, error reporting, suggestions…
                   .UseExceptionHandler()    // catches unhandled exceptions and writes them out
                   .Build();

            return await parsedCommands.InvokeAsync(args);
        }

        private static void PopulateToolHierarchy(RootCommand rootCommand, List<MCPToolBase> toolList)
        {
            var parentMap = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

            foreach (MCPToolBase tool in toolList)
            {
                var subCommands = tool.GetCommandInstances();
                var hierarchy = tool.CommandHierarchy;
                Command previousParent = rootCommand;

                if (hierarchy.Length == 0)
                {
                    // if there is no hierarchy, add the command directly to the root command
                    foreach (var cmd in subCommands)
                    {
                        rootCommand.AddCommand(cmd);
                    }
                    continue;
                }

                for (int i = 0; i < hierarchy.Length; i++)
                {
                    var segment = hierarchy[i];

                    // populate the dictionary lookup for the command so we don't create it multiple times as we walk the hieararchy across multiple tools
                    if (!parentMap.ContainsKey(segment.Verb))
                    {
                        var groupCommand = new Command(segment.Verb, segment.Description);

                        if (segment.Options != null && segment.Options.Count > 0)
                        {
                            foreach (var option in segment.Options)
                            {
                                groupCommand.AddGlobalOption(option);
                            }
                        }

                        parentMap[segment.Verb] = groupCommand;
                    }

                    // now access the node from the dictionary that we're currently on. the previous step populated the parentMap with the Command representing this verb if it didn't already exist
                    var currentNode = parentMap[segment.Verb];

                    // if the previous parent doesn't already have this node, add it, gotta maintain that hierarchy!
                    if (!previousParent.Children.Contains(currentNode))
                    {
                        previousParent.AddCommand(currentNode);
                    }

                    // if we are on the last segment of the hierarchy, add the leaf command to the current node
                    if (i == hierarchy.Length - 1)
                    {
                        // if we're at the end of the hierarchy, add the leaf command
                        foreach (var cmd in subCommands)
                        {
                            currentNode.AddCommand(cmd);
                        }
                    }
                    else
                    {

                        // our previous parent is now the current node for the next iteration
                        previousParent = currentNode;
                    }
                }
            }
        }
    }
}
