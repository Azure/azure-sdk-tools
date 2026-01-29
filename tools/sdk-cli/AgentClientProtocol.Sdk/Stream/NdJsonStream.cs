// Agent Client Protocol - .NET SDK
// Newline-delimited JSON transport (stdio)

using System.Text;
using System.Text.Json;
using AgentClientProtocol.Sdk.JsonRpc;
using Microsoft.Extensions.Logging;

namespace AgentClientProtocol.Sdk.Stream;

/// <summary>
/// Newline-delimited JSON stream for stdio-based ACP communication.
/// </summary>
public class NdJsonStream : IAcpStream
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    public NdJsonStream(TextReader input, TextWriter output, ILogger? logger = null)
    {
        _input = input;
        _output = output;
        _logger = logger;
    }
    
    /// <summary>
    /// Create stream from stdin/stdout.
    /// </summary>
    public static NdJsonStream FromStdio(ILogger? logger = null)
    {
        var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        var output = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
        return new NdJsonStream(input, output, logger);
    }
    
    /// <summary>
    /// Create stream from arbitrary streams.
    /// </summary>
    public static NdJsonStream FromStreams(System.IO.Stream input, System.IO.Stream output, ILogger? logger = null)
    {
        var reader = new StreamReader(input, Encoding.UTF8);
        var writer = new StreamWriter(output, Encoding.UTF8) { AutoFlush = true };
        return new NdJsonStream(reader, writer, logger);
    }
    
    public async ValueTask<JsonRpcMessageBase?> ReadAsync(CancellationToken ct = default)
    {
        var line = await _input.ReadLineAsync(ct);
        if (line == null) return null;
        
        if (string.IsNullOrWhiteSpace(line)) return await ReadAsync(ct);
        
        _logger?.LogTrace("ACP recv: {Message}", line);
        
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            
            // Check if it's a response (has result or error)
            if (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcResponse>(line, JsonOptions);
            }
            
            // Check if it's a request (has id and method)
            if (root.TryGetProperty("id", out _) && root.TryGetProperty("method", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonOptions);
            }
            
            // Otherwise it's a notification (method only)
            if (root.TryGetProperty("method", out _))
            {
                return JsonSerializer.Deserialize<JsonRpcNotification>(line, JsonOptions);
            }
            
            _logger?.LogWarning("Unknown message format: {Message}", line);
            return null;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse message: {Message}", line);
            return null;
        }
    }
    
    public async ValueTask WriteAsync(JsonRpcMessageBase message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), JsonOptions);
        
        await _writeLock.WaitAsync(ct);
        try
        {
            _logger?.LogTrace("ACP send: {Message}", json);
            await _output.WriteLineAsync(json);
        }
        finally
        {
            _writeLock.Release();
        }
    }
    
    public ValueTask CloseAsync()
    {
        _input.Dispose();
        _output.Dispose();
        return ValueTask.CompletedTask;
    }
}
