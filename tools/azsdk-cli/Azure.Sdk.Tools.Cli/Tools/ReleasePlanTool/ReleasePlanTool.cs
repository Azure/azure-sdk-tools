using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Tools.HostServer;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlanTool
{

    [Description("Release Plan Tool type that contains tools to connect to Azure DevOps to get release plan work item")]
    [McpServerToolType]
    public class ReleasePlanTool : MCPTool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReleasePlanTool> _logger;

        public ReleasePlanTool(IServiceProvider serviceProvider, ILogger<ReleasePlanTool> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override Command GetCommand()
        {
            Command command = new Command("get-release-plan");

            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        // HandleCommand is effectively the actual "worker" for the Tool when looked at from the
        // CLI pov. Each individual function marked with attribute [McpServerTool] will themselves
        // be accessible when this class is loaded into assembly and added to MCP configuration in 
        // HostServerTool.CreateAppBuilder
        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // todo: use the ctx to get the arguments that will bind to
            // serviceTreeId, productTreeId, and pullRequestLink
            var releasePlan = GetReleasePlan("a", "b", "c");

            return 0;
        }

        [McpServerTool(Name = "get-release-plan"), Description("Get release plan for a service, product and API spec pull request")]
        public async Task<List<string>> GetReleasePlan(string serviceTreeId, string productTreeId, string pullRequestLink)
        {
            // todo: once we get buy in from benbp/wesh we should move tools/mcp/dotnet/AzureSDKDevToolsMCP/Tools/ReleasePlanTool.cs here.
            // todo: same for `SpecPullRequestTool.cs` and `SpecValidationTool.cs`

            return new List<string> { "Hello", "World" };
        }
    }
}
