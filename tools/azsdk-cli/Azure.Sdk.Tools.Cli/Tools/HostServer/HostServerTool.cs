using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Commands;
using ModelContextProtocol.Server;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Sdk.Tools.Cli.Tools.HostServer
{
    public class HostServerTool : MCPTool
    {
        private readonly ILogger<HostServerTool> _logger;
        private readonly Argument<string[]> _unmatchedAspNetArguments = new Argument<string[]>();

        public HostServerTool(ILogger<HostServerTool> logger)
        {
            _logger = logger;
        }

        public override Command GetCommand()
        {
            Command command = new Command("start");
            command.AddArgument(_unmatchedAspNetArguments);

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var unmatched = ctx.ParseResult.GetValueForArgument<string[]>(_unmatchedAspNetArguments);
            var toolsOption = ctx.ParseResult.GetValueForOption<string>(SharedOptions.ToolOption);
            var tools = toolsOption != null
                ? toolsOption.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : new string[] { };

            var host = CreateAppBuilder(tools, unmatched).Build();
            try
            {
                await host.RunAsync(ct);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during web app run: {ex}", ex);
                return 1;
            }
        }

        public static WebApplicationBuilder CreateAppBuilder(string[] tools, string[] unmatchedArgs)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(unmatchedArgs);

            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });

            ServiceRegistrations.RegisterCommonServices(builder.Services);

            // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
            // behind a command line flag or we could try to run in both modes at once if possible.
            //.WithHttpTransport()

            if (tools.Length == 0)
            {
                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithToolsFromAssembly();
            }
            else
            {
                var toolTypes = SharedOptions.GetFilteredToolTypes(tools);

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithTools(toolTypes);
            }

            return builder;
        }
    }
}
