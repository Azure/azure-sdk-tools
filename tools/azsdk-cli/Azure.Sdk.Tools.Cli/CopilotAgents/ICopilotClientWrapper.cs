using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Represents the authentication status of the Copilot client.
/// </summary>
/// <param name="IsAuthenticated">Whether the user is authenticated.</param>
public record CopilotAuthStatus(bool IsAuthenticated);

/// <summary>
/// Interface wrapper for CopilotClient to enable unit testing.
/// </summary>
public interface ICopilotClientWrapper
{
    Task<ICopilotSessionWrapper> CreateSessionAsync(
        SessionConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authentication status of the Copilot client.
    /// </summary>
    Task<CopilotAuthStatus> GetAuthStatusAsync(CancellationToken cancellationToken = default);
}
