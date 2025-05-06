using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Tools.HostServer;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.HelloWorldTool
{

    [McpServerToolType, Description("Echoes the message back to the client.")]
    public class HelloWorldTool : MCPTool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HelloWorldTool> _logger;

        public HelloWorldTool(IServiceProvider serviceProvider, ILogger<HelloWorldTool> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Command GetCommand()
        {
            Command command = new Command("hello-world");

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int>HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // ctx.getNamedOption<string>("msg") or the like eventually. for now just filler
            var result = Echo("abc123");

            // eventually we want to ensure that we use @result for complex objects as well
            // lets start off with best logging practices if we can
            _logger.LogInformation("Echoing {result}", result);

            return 0;
        }

        [McpServerTool(Name="hello-world"), Description("Echoes the message back to the client.")]
        public static string Echo(string message) => $"RESPONDING TO {message}";
    }
}
