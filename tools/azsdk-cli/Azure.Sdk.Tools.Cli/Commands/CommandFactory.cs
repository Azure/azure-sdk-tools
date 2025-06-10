using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public static class CommandFactory
    {

        /// <summary>
        /// Creates the primary parsing entry point for the application. Uses the registered service providers
        /// to initialize whichever MCP tools we need to add to the configuration and pass on to HostTool.
        /// </summary>
        /// <returns></returns>
        public static RootCommand CreateRootCommand(string[] args, IServiceProvider serviceProvider)
        {
            var rootCommand = new RootCommand("azsdk cli - A Model Context Protocol (MCP) server that enables various tasks for the Azure SDK Engineering System.");
            rootCommand.AddOption(SharedOptions.ToolOption);

            rootCommand.AddGlobalOption(SharedOptions.Debug);

            SharedOptions.Format.AddValidator(result =>
            {
                var value = result.GetValueForOption(SharedOptions.Format);
                if (value != "plain" && value != "json")
                {
                    result.ErrorMessage = $"Invalid output format '{value}'. Supported formats are: plain, json";
                }
            });
            rootCommand.AddGlobalOption(SharedOptions.Format);

            var toolTypes = SharedOptions.GetFilteredToolTypes(args);

            var toolInstances = toolTypes
                .Select(t => (MCPTool)ActivatorUtilities.CreateInstance(serviceProvider, t))
                .ToList();

            PopulateToolHierarchy(rootCommand, toolInstances);

            return rootCommand;
        }

        private static void PopulateToolHierarchy(RootCommand rootCommand, List<MCPTool> toolList)
        {
            var parentMap = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

            foreach (MCPTool tool in toolList)
            {
                var leaf = tool.GetCommand();
                var hierarchy = tool.CommandHierarchy;
                Command previousParent = rootCommand;

                if (hierarchy.Length == 0)
                {
                    // if there is no hierarchy, add the command directly to the root command
                    rootCommand.AddCommand(leaf);
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
                        currentNode.AddCommand(leaf);
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
