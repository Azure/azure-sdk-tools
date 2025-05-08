using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.Reflection;
using System.IO.Enumeration;

namespace Azure.Sdk.Tools.Cli.Commands
{
    public class CommandFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommandFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public CommandFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<CommandFactory>();
            _loggerFactory = loggerFactory;
        }

        private List<Type> GetFilteredToolTypes(string[] args)
        {
            var toolMatchList = SharedOptions.GetToolsFromArgs(args);
            List<Type> toolsList;

            if (toolMatchList.Count > 0)
            {
                toolsList = AppDomain.CurrentDomain
                             .GetAssemblies()
                             .SelectMany(a => SafeGetTypes(a))
                             .Where(t => !t.IsAbstract &&
                             typeof(MCPTool).IsAssignableFrom(t))
                             .Where(t => toolMatchList.Any(x => FileSystemName.MatchesSimpleExpression(x, t.Name)))
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

        /// <summary>
        /// Creates the primary parsing entry point for the application. Uses the registered service providers
        /// to initialize whichever MCP tools we need to add to the configuration and pass on to HostTool.
        /// </summary>
        /// <returns></returns>
        public RootCommand CreateRootCommand(string[] args)
        {
            var rootCommand = new RootCommand("azsdk cli - A Model Context Protocol (MCP) server that enables various tasks for the Azure SDK Engineering System.");
            rootCommand.AddOption(SharedOptions.ToolOption);

            var toolTypes = GetFilteredToolTypes(args);

            // walk the tools, register them as subcommands for the root command.
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
