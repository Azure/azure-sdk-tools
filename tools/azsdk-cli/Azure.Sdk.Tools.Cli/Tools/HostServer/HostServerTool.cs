// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Commands;

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
            Command command = new Command("start", "Starts the web server");
            command.SetHandler(async ctx =>
            {
                ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
            });

            return command;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {

            try
            {
                await Program.ServerApp.RunAsync(ct);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during web app run: {ex}", ex);
                return 1;
            }
        }
    }
}
