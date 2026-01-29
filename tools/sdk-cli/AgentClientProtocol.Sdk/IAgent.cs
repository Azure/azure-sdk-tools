// Agent Client Protocol - .NET SDK
// Agent interface

using AgentClientProtocol.Sdk.Schema;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Interface that all ACP-compliant agents must implement.
/// 
/// Agents are programs that use generative AI to autonomously modify code.
/// They handle requests from clients (IDEs) and execute tasks using language models and tools.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Establishes the connection and negotiates protocol capabilities.
    /// </summary>
    Task<InitializeResponse> InitializeAsync(InitializeRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Creates a new conversation session.
    /// </summary>
    Task<NewSessionResponse> NewSessionAsync(NewSessionRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Processes a user prompt within a session.
    /// </summary>
    Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Handles session cancellation.
    /// </summary>
    Task CancelAsync(CancelNotification notification, CancellationToken ct = default);
    
    /// <summary>
    /// Authenticates the client (optional).
    /// </summary>
    Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct = default) =>
        Task.FromResult<AuthenticateResponse?>(null);
    
    /// <summary>
    /// Loads an existing session (optional).
    /// </summary>
    Task<LoadSessionResponse?> LoadSessionAsync(LoadSessionRequest request, CancellationToken ct = default) =>
        Task.FromResult<LoadSessionResponse?>(null);
}
