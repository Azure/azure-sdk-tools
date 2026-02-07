// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Services.Upgrade;

/// <summary>
/// Hosted service that checks for upgrades after the MCP server has started.
/// This ensures upgrade notification logs are properly forwarded through the MCP protocol.
/// </summary>

public sealed class UpgradeNotificationHostedService(
#if !DEBUG
    IUpgradeService upgradeService
#endif
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
#if !DEBUG
            await upgradeService.TryShowUpgradeNotification(cancellationToken);
#else
            // No point saying "there's a new version" when people are developing via dotnet run!
            return;
#endif
        }
        catch
        {
            // Ignore update notification errors - never affect server startup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
