using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Production wrapper that delegates to the actual CopilotClient.
/// </summary>
public class CopilotClientWrapper(CopilotClient client) : ICopilotClientWrapper
{
    public async Task<ICopilotSessionWrapper> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default)
    {
        var session = await client.CreateSessionAsync(config, cancellationToken);
        return new CopilotSessionWrapper(session);
    }
}
