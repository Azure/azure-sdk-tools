using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Contract;
using System.Reflection;
using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using ModelContextProtocol.Protocol.Types;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public class CommandFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private readonly RootCommand _rootCommand;

        public CommandFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<CommandFactory>();
            _loggerFactory = loggerFactory;
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

            // walk the tools, register them as subcommands for the root command.
            var toolTypes = AppDomain.CurrentDomain
                         .GetAssemblies()
                         .SelectMany(a => SafeGetTypes(a))
                         .Where(t => !t.IsAbstract &&
                         typeof(MCPTool).IsAssignableFrom(t))
                         .ToList();

            // todo: we need to check the constructors here, and add any services to the bundle that may be necessary

            foreach (var t in toolTypes)
            {
                var tool = (MCPTool)ActivatorUtilities.CreateInstance(_serviceProvider, t);
                rootCommand.AddCommand(tool.GetCommand());
                _logger.LogDebug("Mapped tool {Tool}", t.FullName);
            }

            return rootCommand;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                return ex.Types!.Where(t => t != null)!;
            }
        }
    }
}
