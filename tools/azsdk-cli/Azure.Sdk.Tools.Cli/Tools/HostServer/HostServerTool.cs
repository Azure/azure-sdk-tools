using Azure.Sdk.Tools.Cli.Services.Azure;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Commands;

namespace Azure.Sdk.Tools.Cli.Tools.HostServer
{
    public class HostServerTool : MCPTool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HostServerTool> _logger;

        public HostServerTool(IServiceProvider serviceProvider, ILogger<HostServerTool> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Command GetCommand()
        {
            Command command = new Command("start");

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // ctx gives us access to options via getNamedOption() and we can pass the arguments that are used
            // were passed our command creation in GetCommand()

            // todo: should probably actually read out the unmatched args here like we do in test-proxy
            var host = CreateAppBuilder(new string[]{}).Build();
            await host.RunAsync(ct);

            return 0;
        }

        public static WebApplicationBuilder CreateAppBuilder(string[] args)
        {
            // todo: implement our own module discovery that takes the `--tools` or `--tools-exclude` when booting
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Error;
            });
            builder.Services.AddSingleton<IAzureService, AzureService>();

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                // For testing SSE can be easier to use. Comment above and uncomment below. Eventually this will be
                // behind a command line flag or we could try to run in both modes at once if possible.
                //.WithHttpTransport()
                // todo: we can definitely honor the --tools param here to filter down the provided tools
                // for now, lets just use WithtoolsFromAssembly to actually run this thing
                .WithToolsFromAssembly();

            return builder;
        }
    }
}
