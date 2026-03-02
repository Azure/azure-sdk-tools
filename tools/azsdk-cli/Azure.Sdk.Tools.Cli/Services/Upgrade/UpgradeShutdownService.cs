using System.Threading.Channels;

namespace Azure.Sdk.Tools.Cli.Services.Upgrade;

public sealed class UpgradeShutdownCoordinator
{
    private readonly Channel<bool> requests = Channel.CreateUnbounded<bool>();

    public ValueTask RequestShutdown() => requests.Writer.WriteAsync(true);
    public IAsyncEnumerable<bool> Watch(CancellationToken ct) => requests.Reader.ReadAllAsync(ct);
}

public sealed class UpgradeShutdownService(
    UpgradeShutdownCoordinator coord,
    IHostApplicationLifetime lifetime,
    ILogger<UpgradeShutdownService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in coord.Watch(stoppingToken))
        {
            // Let the MCP stack finish writing the tool response.
            await Task.Yield();

            // Best-effort flush/close stdout (stdio shutdown signal per spec).
            try { await Console.Out.FlushAsync(stoppingToken); } catch { /* ignore */ }
            try { Console.Out.Close(); } catch { /* ignore */ }

            log.LogInformation("Upgrade requested; stopping host.");
            lifetime.StopApplication();

            // Wait a moment to allow the final response to be received by the MCP client
            await Task.Delay(1000, stoppingToken);
        }
    }
}
