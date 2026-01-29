using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Interface wrapper for CopilotClient to enable unit testing.
/// </summary>
public interface ICopilotClientWrapper
{
    Task<ICopilotSessionWrapper> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default);
}
