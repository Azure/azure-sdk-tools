// Agent Client Protocol - .NET SDK
// Capability types for initialization

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Capabilities supported by the agent.
/// </summary>
public record AgentCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("loadSession")]
    public bool? LoadSession { get; init; }
    
    [JsonPropertyName("mcpCapabilities")]
    public McpCapabilities? McpCapabilities { get; init; }
    
    [JsonPropertyName("promptCapabilities")]
    public PromptCapabilities? PromptCapabilities { get; init; }
    
    [JsonPropertyName("sessionCapabilities")]
    public SessionCapabilities? SessionCapabilities { get; init; }
}

/// <summary>
/// Capabilities supported by the client.
/// </summary>
public record ClientCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("fs")]
    public FileSystemCapability? Fs { get; init; }
    
    [JsonPropertyName("terminal")]
    public bool? Terminal { get; init; }
}

/// <summary>
/// File system capabilities.
/// </summary>
public record FileSystemCapability
{
    [JsonPropertyName("readTextFile")]
    public bool ReadTextFile { get; init; }
    
    [JsonPropertyName("writeTextFile")]
    public bool WriteTextFile { get; init; }
}

/// <summary>
/// MCP capabilities supported by the agent.
/// </summary>
public record McpCapabilities
{
    [JsonPropertyName("http")]
    public bool? Http { get; init; }
    
    [JsonPropertyName("sse")]
    public bool? Sse { get; init; }
}

/// <summary>
/// Prompt capabilities.
/// </summary>
public record PromptCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Session capabilities.
/// </summary>
public record SessionCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("fork")]
    public SessionForkCapabilities? Fork { get; init; }
    
    [JsonPropertyName("list")]
    public SessionListCapabilities? List { get; init; }
    
    [JsonPropertyName("resume")]
    public SessionResumeCapabilities? Resume { get; init; }
}

public record SessionForkCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

public record SessionListCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

public record SessionResumeCapabilities
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Metadata about implementation.
/// </summary>
public record Implementation
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }
}
