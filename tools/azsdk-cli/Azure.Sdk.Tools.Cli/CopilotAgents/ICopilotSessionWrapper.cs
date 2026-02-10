using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Interface wrapper for CopilotSession to enable unit testing.
/// </summary>
public interface ICopilotSessionWrapper : IAsyncDisposable
{
    IDisposable On(SessionEventHandler handler);
    Task<string> SendAsync(MessageOptions options, CancellationToken cancellationToken = default);
}
