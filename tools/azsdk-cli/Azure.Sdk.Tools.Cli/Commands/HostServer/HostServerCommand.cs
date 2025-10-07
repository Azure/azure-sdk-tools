// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Commands.HostServer
{
    public class HostServerCommand(ILogger<HostServerCommand> logger, IRawOutputHelper outputHelper)
    {
        public Command GetCommand()
        {
            Command cmd = new("mcp", "Starts the MCP server (stdio mode)");
            cmd.Aliases.Add("start");  // backwards compatibility
            cmd.SetAction((_, cancellationToken) => HandleCommand(cancellationToken));
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
