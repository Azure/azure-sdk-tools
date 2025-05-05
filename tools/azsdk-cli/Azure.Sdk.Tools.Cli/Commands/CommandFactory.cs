using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public class CommandFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandFactory> _logger;
        private readonly RootCommand _rootCommand;

        public CommandFactory(IServiceProvider serviceProvider, ILogger<CommandFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _rootCommand = CreateRootCommand();
        }

        /// <summary>
        /// Creates the primary parsing entry point for the application. Uses the registered service providers
        /// to initialize whichever MCP tools we need to add to the configuration and pass on to HostTool.
        /// </summary>
        /// <returns></returns>
        public RootCommand CreateRootCommand()
        {
            var rootCommand = new RootCommand("azsdk cli - A Model Context Protocol (MCP) server that enables various tasks for the Azure SDK Engineering System.");

            // walk the various assembly items, adding their subcommands to the root command
            return rootCommand;
        }
    }
}
