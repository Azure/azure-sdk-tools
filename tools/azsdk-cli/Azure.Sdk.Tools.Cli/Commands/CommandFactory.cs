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

            // walk the tools, register them as subcommands for the root command.
            foreach (var t in toolTypes)
            {
                var tool = (MCPTool)ActivatorUtilities.CreateInstance(serviceProvider, t);
                rootCommand.AddCommand(tool.GetCommand());
            }

            return rootCommand;
        }
    }
}
