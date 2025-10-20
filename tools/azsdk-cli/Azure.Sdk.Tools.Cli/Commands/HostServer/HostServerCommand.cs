// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Commands.HostServer
{
    public class HostServerCommand(ILogger<HostServerCommand> logger, IRawOutputHelper outputHelper)
    {
        private static readonly Option<string> toolOption = new("--tools")
        {
            Description = "If provided, the mcp server will only list and respond to tools named the same as provided in this option. Glob matching is honored.",
            Required = false,
        };

        public Command GetCommand()
        {
            Command cmd = new("mcp", "Starts the MCP server (stdio mode)") { toolOption };
            Command legacyStartCmd = new("start", "Starts the MCP server (stdio mode)") { toolOption };
            legacyStartCmd.Hidden = true;
            cmd.SetAction((_, cancellationToken) => HandleCommand(cancellationToken));
            legacyStartCmd.SetAction((_, cancellationToken) => HandleCommand(cancellationToken));
            return cmd;
        }

        public async Task<int> HandleCommand(CancellationToken ct)
        {
            try
            {
                await Program.ServerApp.RunAsync(ct);
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during web app run");
                outputHelper.OutputConsoleError($"Exception during web app run: {ex.Message}");
                return 1;
            }
        }
    }
}
