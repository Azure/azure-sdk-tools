// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class McpLoggingHostedService(McpServer server, IMcpServerContextAccessor contextAccessor) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        contextAccessor.Initialize(server);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        contextAccessor.Disable();
        return Task.CompletedTask;
    }
}
