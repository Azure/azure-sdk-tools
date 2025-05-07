using Azure.Sdk.Tools.Cli.Services.Azure;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace Azure.Sdk.Tools.Cli.Tools.HostServer
{
    public class HostServerTool : MCPTool
    {
        private readonly ILogger<HostServerTool> _logger;

        public HostServerTool(ILogger<HostServerTool> logger)
        {
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
            // todo: should probably actually read out the unmatched args here like we do in test-proxy to grab the ASP.NET arguments
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
            builder.Services.AddSingleton<IGitHubService, GitHubService>();
            builder.Services.AddSingleton<IGitHelper, GitHelper>();
            builder.Services.AddSingleton<ITypeSpecHelper, TypeSpecHelper>();
            builder.Services.AddSingleton<IDevOpsConnection, DevOpsConnection>();
            builder.Services.AddSingleton<IDevOpsService, DevOpsService>();

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
