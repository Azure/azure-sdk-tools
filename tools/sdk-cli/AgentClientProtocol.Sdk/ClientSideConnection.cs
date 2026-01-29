// Agent Client Protocol - .NET SDK
// Client's view of the ACP connection

using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using AgentClientProtocol.Sdk.Schema;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk;

/// <summary>
/// Client-side connection to an agent.
/// 
/// Provides the client's view of an ACP connection, allowing clients (IDEs)
/// to communicate with agents. Implements the IAgent interface to provide methods
/// for initializing sessions, sending prompts, and managing the agent lifecycle.
/// </summary>
public class ClientSideConnection : IAgent, IAsyncDisposable
{
    private readonly Connection _connection;
    private readonly ILogger? _logger;
    
    public ClientSideConnection(IClient client, IAcpStream stream, ILogger? logger = null)
    {
        _logger = logger;
        _connection = new Connection(stream, logger);
        
        _connection.OnRequest(async (method, @params) =>
        {
            var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
            
            return method switch
            {
                ClientMethods.SessionRequestPermission => await client.RequestPermissionAsync(Deserialize<RequestPermissionRequest>(json)),
                ClientMethods.FsReadTextFile => await client.ReadTextFileAsync(Deserialize<ReadTextFileRequest>(json)),
                ClientMethods.FsWriteTextFile => await client.WriteTextFileAsync(Deserialize<WriteTextFileRequest>(json)),
                ClientMethods.TerminalCreate => await client.CreateTerminalAsync(Deserialize<CreateTerminalRequest>(json)),
                ClientMethods.TerminalOutput => await client.TerminalOutputAsync(Deserialize<TerminalOutputRequest>(json)),
                ClientMethods.TerminalRelease => await client.ReleaseTerminalAsync(Deserialize<ReleaseTerminalRequest>(json)),
                ClientMethods.TerminalWaitForExit => await client.WaitForTerminalExitAsync(Deserialize<WaitForTerminalExitRequest>(json)),
                ClientMethods.TerminalKill => await client.KillTerminalAsync(Deserialize<KillTerminalCommandRequest>(json)),
                _ => throw RequestError.MethodNotFound(method)
            };
        });
        
        _connection.OnNotification(async (method, @params) =>
        {
            if (method == ClientMethods.SessionUpdate)
            {
                var json = @params is JsonElement el ? el : JsonSerializer.SerializeToElement(@params);
                await client.SessionUpdateAsync(Deserialize<SessionNotification>(json));
            }
        });
    }
    
    private static T Deserialize<T>(JsonElement? element) =>
        element.HasValue 
            ? JsonSerializer.Deserialize<T>(element.Value.GetRawText())! 
            : throw RequestError.InvalidParams(null, "Missing parameters");
    
    /// <summary>
    /// Start processing messages.
    /// </summary>
    public Task RunAsync(CancellationToken ct = default) => _connection.RunAsync(ct);
    
    // IAgent implementation - calls to the agent
    
    public Task<InitializeResponse> InitializeAsync(InitializeRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<InitializeResponse>(AgentMethods.Initialize, request, ct)!;
    
    public Task<AuthenticateResponse?> AuthenticateAsync(AuthenticateRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<AuthenticateResponse>(AgentMethods.Authenticate, request, ct);
    
    public Task<NewSessionResponse> NewSessionAsync(NewSessionRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<NewSessionResponse>(AgentMethods.SessionNew, request, ct)!;
    
    public Task<LoadSessionResponse?> LoadSessionAsync(LoadSessionRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<LoadSessionResponse>(AgentMethods.SessionLoad, request, ct);
    
    public Task<PromptResponse> PromptAsync(PromptRequest request, CancellationToken ct = default) =>
        _connection.SendRequestAsync<PromptResponse>(AgentMethods.SessionPrompt, request, ct)!;
    
    public Task CancelAsync(CancelNotification notification, CancellationToken ct = default) =>
        _connection.SendNotificationAsync(AgentMethods.SessionCancel, notification, ct);
    
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
