// Agent Client Protocol - .NET SDK
// Session-related types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request to initialize connection.
/// </summary>
public record InitializeRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }
    
    [JsonPropertyName("clientCapabilities")]
    public ClientCapabilities? ClientCapabilities { get; init; }
    
    [JsonPropertyName("clientInfo")]
    public Implementation? ClientInfo { get; init; }
}

/// <summary>
/// Response to initialize request.
/// </summary>
public record InitializeResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }
    
    [JsonPropertyName("agentCapabilities")]
    public AgentCapabilities? AgentCapabilities { get; init; }
    
    [JsonPropertyName("agentInfo")]
    public Implementation? AgentInfo { get; init; }
    
    [JsonPropertyName("authMethods")]
    public AuthMethod[]? AuthMethods { get; init; }
}

/// <summary>
/// Authentication method.
/// </summary>
public record AuthMethod
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Request to authenticate.
/// </summary>
public record AuthenticateRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("methodId")]
    public required string MethodId { get; init; }
}

/// <summary>
/// Response to authenticate request.
/// </summary>
public record AuthenticateResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Request to create a new session.
/// </summary>
public record NewSessionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }
    
    [JsonPropertyName("mcpServers")]
    public McpServer[]? McpServers { get; init; }
}

/// <summary>
/// Response to new session request.
/// </summary>
public record NewSessionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("modes")]
    public SessionModeState? Modes { get; init; }
}

/// <summary>
/// Request to load an existing session.
/// </summary>
public record LoadSessionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }
    
    [JsonPropertyName("mcpServers")]
    public McpServer[]? McpServers { get; init; }
}

/// <summary>
/// Response to load session request.
/// </summary>
public record LoadSessionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Request to prompt in a session.
/// </summary>
public record PromptRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("prompt")]
    public required ContentBlock[] Prompt { get; init; }
}

/// <summary>
/// Response to prompt request.
/// </summary>
public record PromptResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("stopReason")]
    public required string StopReason { get; init; }
}

/// <summary>
/// Cancel notification.
/// </summary>
public record CancelNotification
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
}

/// <summary>
/// MCP server configuration.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpServerStdio), "stdio")]
[JsonDerivedType(typeof(McpServerHttp), "http")]
[JsonDerivedType(typeof(McpServerSse), "sse")]
public abstract record McpServer
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public record McpServerStdio : McpServer
{
    [JsonPropertyName("command")]
    public required string Command { get; init; }
    
    [JsonPropertyName("args")]
    public string[]? Args { get; init; }
    
    [JsonPropertyName("env")]
    public EnvVariable[]? Env { get; init; }
}

public record McpServerHttp : McpServer
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("headers")]
    public HttpHeader[]? Headers { get; init; }
}

public record McpServerSse : McpServer
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    
    [JsonPropertyName("headers")]
    public HttpHeader[]? Headers { get; init; }
}

public record EnvVariable
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

public record HttpHeader
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>
/// Session mode state.
/// </summary>
public record SessionModeState
{
    [JsonPropertyName("availableModes")]
    public required SessionMode[] AvailableModes { get; init; }
    
    [JsonPropertyName("currentModeId")]
    public required string CurrentModeId { get; init; }
}

/// <summary>
/// Session mode.
/// </summary>
public record SessionMode
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Stop reasons for prompt responses.
/// </summary>
public static class StopReason
{
    public const string EndTurn = "end_turn";
    public const string MaxTokens = "max_tokens";
    public const string StopSequence = "stop_sequence";
    public const string Cancelled = "cancelled";
}
