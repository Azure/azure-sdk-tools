using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Production wrapper that delegates to the actual CopilotSession.
/// </summary>
public class CopilotSessionWrapper(CopilotSession session) : ICopilotSessionWrapper
{

    public IDisposable On(SessionEventHandler handler)
    {
        return session.On(handler);
    }

    public Task<string> SendAsync(MessageOptions options, CancellationToken cancellationToken = default)
    {
        return session.SendAsync(options, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return session.DisposeAsync();
    }
}
