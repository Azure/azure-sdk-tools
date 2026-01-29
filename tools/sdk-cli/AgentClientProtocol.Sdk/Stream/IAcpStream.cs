// Agent Client Protocol - .NET SDK
// Stream abstraction

using AgentClientProtocol.Sdk.JsonRpc;

namespace AgentClientProtocol.Sdk.Stream;

/// <summary>
/// Bidirectional stream for ACP communication.
/// </summary>
public interface IAcpStream
{
    /// <summary>
    /// Read the next message from the stream.
    /// </summary>
    ValueTask<JsonRpcMessageBase?> ReadAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write a message to the stream.
    /// </summary>
    ValueTask WriteAsync(JsonRpcMessageBase message, CancellationToken ct = default);
    
    /// <summary>
    /// Close the stream.
    /// </summary>
    ValueTask CloseAsync();
}
