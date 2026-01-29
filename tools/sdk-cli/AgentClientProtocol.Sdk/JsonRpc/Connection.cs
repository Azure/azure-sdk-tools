// Agent Client Protocol - .NET SDK
// Low-level JSON-RPC connection management

using System.Collections.Concurrent;
using AgentClientProtocol.Sdk.Stream;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk.JsonRpc;

/// <summary>
/// Manages bidirectional JSON-RPC communication.
/// </summary>
public class Connection : IAsyncDisposable
{
    private readonly IAcpStream _stream;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonRpcResponse>> _pendingRequests = new();
    private readonly CancellationTokenSource _cts = new();
    private int _requestId = 0;
    
    private Func<string, object?, Task<object?>>? _requestHandler;
    private Func<string, object?, Task>? _notificationHandler;
    
    public Connection(IAcpStream stream, ILogger? logger = null)
    {
        _stream = stream;
        _logger = logger;
    }
    
    /// <summary>
    /// Set handler for incoming requests.
    /// </summary>
    public void OnRequest(Func<string, object?, Task<object?>> handler)
    {
        _requestHandler = handler;
    }
    
    /// <summary>
    /// Set handler for incoming notifications.
    /// </summary>
    public void OnNotification(Func<string, object?, Task> handler)
    {
        _notificationHandler = handler;
    }
    
    /// <summary>
    /// Start processing incoming messages.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        
        try
        {
            while (!linked.Token.IsCancellationRequested)
            {
                var message = await _stream.ReadAsync(linked.Token);
                if (message == null) break;
                
                _ = Task.Run(() => HandleMessageAsync(message), linked.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection error");
        }
    }
    
    private async Task HandleMessageAsync(JsonRpcMessageBase message)
    {
        switch (message)
        {
            case JsonRpcRequest request:
                await HandleRequestAsync(request);
                break;
            case JsonRpcResponse response:
                HandleResponse(response);
                break;
            case JsonRpcNotification notification:
                await HandleNotificationAsync(notification);
                break;
        }
    }
    
    private async Task HandleRequestAsync(JsonRpcRequest request)
    {
        if (_requestHandler == null)
        {
            await SendErrorAsync(request.Id, RequestError.MethodNotFound(request.Method));
            return;
        }
        
        try
        {
            var result = await _requestHandler(request.Method, request.Params);
            await SendResponseAsync(request.Id, result);
        }
        catch (RequestError ex)
        {
            await SendErrorAsync(request.Id, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Request handler error for {Method}", request.Method);
            await SendErrorAsync(request.Id, RequestError.InternalError(null, ex.Message));
        }
    }
    
    private void HandleResponse(JsonRpcResponse response)
    {
        if (response.Id != null && _pendingRequests.TryRemove(response.Id, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }
    
    private async Task HandleNotificationAsync(JsonRpcNotification notification)
    {
        if (_notificationHandler != null)
        {
            try
            {
                await _notificationHandler(notification.Method, notification.Params);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Notification handler error for {Method}", notification.Method);
            }
        }
    }
    
    /// <summary>
    /// Send a request and wait for response.
    /// </summary>
    public async Task<T?> SendRequestAsync<T>(string method, object? parameters = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _requestId);
        var tcs = new TaskCompletionSource<JsonRpcResponse>();
        _pendingRequests[id] = tcs;
        
        try
        {
            var request = new JsonRpcRequest
            {
                Id = id,
                Method = method,
                Params = parameters != null ? System.Text.Json.JsonSerializer.SerializeToElement(parameters) : null
            };
            
            await _stream.WriteAsync(request, ct);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            
            var response = await tcs.Task.WaitAsync(cts.Token);
            
            if (response.Error != null)
            {
                throw new RequestError(response.Error.Code, response.Error.Message, response.Error.Data);
            }
            
            if (response.Result == null) return default;
            
            return System.Text.Json.JsonSerializer.Deserialize<T>(
                System.Text.Json.JsonSerializer.Serialize(response.Result));
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }
    
    /// <summary>
    /// Send a notification (no response expected).
    /// </summary>
    public async Task SendNotificationAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        var notification = new JsonRpcNotification
        {
            Method = method,
            Params = parameters
        };
        await _stream.WriteAsync(notification, ct);
    }
    
    private async Task SendResponseAsync(object? id, object? result)
    {
        var response = JsonRpcResponse.Success(id, result);
        await _stream.WriteAsync(response);
    }
    
    private async Task SendErrorAsync(object? id, RequestError error)
    {
        var response = error.ToResponse(id);
        await _stream.WriteAsync(response);
    }
    
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _stream.CloseAsync();
        _cts.Dispose();
    }
}
