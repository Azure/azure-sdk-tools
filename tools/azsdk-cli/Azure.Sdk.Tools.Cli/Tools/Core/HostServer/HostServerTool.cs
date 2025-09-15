// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.HostServer
{
    public class HostServerTool : MCPTool
    {
        private readonly ILogger<HostServerTool> _logger;

        public HostServerTool(ILogger<HostServerTool> logger)
        {
            _logger = logger;
        }

        protected override Command GetCommand() => new("start", "Starts the MCP server (stdio mode)");

        public override async Task<CommandResponse> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                await Program.ServerApp.RunAsync(ct);
                return new DefaultCommandResponse { };
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception during web app run: {ex}", ex);
                return new DefaultCommandResponse { ResponseError = ex.Message };
            }
        }
    }
}
