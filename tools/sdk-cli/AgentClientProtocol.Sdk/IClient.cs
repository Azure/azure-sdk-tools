// Agent Client Protocol - .NET SDK
// Client interface

using AgentClientProtocol.Sdk.Schema;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Interface that ACP-compliant clients must implement.
/// 
/// Clients are typically code editors (IDEs) that provide the interface
/// between users and AI agents. They manage the environment, handle user interactions,
/// and control access to resources.
/// </summary>
public interface IClient
{
    /// <summary>
    /// Handle permission request from agent.
    /// </summary>
    Task<RequestPermissionResponse> RequestPermissionAsync(RequestPermissionRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Handle session update from agent.
    /// </summary>
    Task SessionUpdateAsync(SessionNotification notification, CancellationToken ct = default);
    
    /// <summary>
    /// Read a text file (optional).
    /// </summary>
    Task<ReadTextFileResponse?> ReadTextFileAsync(ReadTextFileRequest request, CancellationToken ct = default) =>
        Task.FromResult<ReadTextFileResponse?>(null);
    
    /// <summary>
    /// Write a text file (optional).
    /// </summary>
    Task<WriteTextFileResponse?> WriteTextFileAsync(WriteTextFileRequest request, CancellationToken ct = default) =>
        Task.FromResult<WriteTextFileResponse?>(null);
    
    /// <summary>
    /// Create a terminal (optional).
    /// </summary>
    Task<CreateTerminalResponse?> CreateTerminalAsync(CreateTerminalRequest request, CancellationToken ct = default) =>
        Task.FromResult<CreateTerminalResponse?>(null);
    
    /// <summary>
    /// Get terminal output (optional).
    /// </summary>
    Task<TerminalOutputResponse?> TerminalOutputAsync(TerminalOutputRequest request, CancellationToken ct = default) =>
        Task.FromResult<TerminalOutputResponse?>(null);
    
    /// <summary>
    /// Release a terminal (optional).
    /// </summary>
    Task<ReleaseTerminalResponse?> ReleaseTerminalAsync(ReleaseTerminalRequest request, CancellationToken ct = default) =>
        Task.FromResult<ReleaseTerminalResponse?>(null);
    
    /// <summary>
    /// Wait for terminal exit (optional).
    /// </summary>
    Task<WaitForTerminalExitResponse?> WaitForTerminalExitAsync(WaitForTerminalExitRequest request, CancellationToken ct = default) =>
        Task.FromResult<WaitForTerminalExitResponse?>(null);
    
    /// <summary>
    /// Kill terminal command (optional).
    /// </summary>
    Task<KillTerminalCommandResponse?> KillTerminalAsync(KillTerminalCommandRequest request, CancellationToken ct = default) =>
        Task.FromResult<KillTerminalCommandResponse?>(null);
}
