// Agent Client Protocol - .NET SDK
// Content types for messages

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Base content block in prompts and responses.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContent), "text")]
[JsonDerivedType(typeof(ImageContent), "image")]
[JsonDerivedType(typeof(AudioContent), "audio")]
[JsonDerivedType(typeof(EmbeddedResource), "resource")]
public abstract record ContentBlock;

/// <summary>
/// Text content.
/// </summary>
public record TextContent : ContentBlock
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Image content.
/// </summary>
public record ImageContent : ContentBlock
{
    [JsonPropertyName("data")]
    public required string Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Audio content.
/// </summary>
public record AudioContent : ContentBlock
{
    [JsonPropertyName("data")]
    public required string Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Embedded resource content.
/// </summary>
public record EmbeddedResource : ContentBlock
{
    [JsonPropertyName("resource")]
    public required ResourceContents Resource { get; init; }
    
    [JsonPropertyName("annotations")]
    public Annotations? Annotations { get; init; }
}

/// <summary>
/// Resource contents (text or blob).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextResourceContents), "text")]
[JsonDerivedType(typeof(BlobResourceContents), "blob")]
public abstract record ResourceContents
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

public record TextResourceContents : ResourceContents
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

public record BlobResourceContents : ResourceContents
{
    [JsonPropertyName("blob")]
    public required string Blob { get; init; }
}

/// <summary>
/// Annotations for content.
/// </summary>
public record Annotations
{
    [JsonPropertyName("audience")]
    public string[]? Audience { get; init; }
    
    [JsonPropertyName("priority")]
    public double? Priority { get; init; }
    
    [JsonPropertyName("lastModified")]
    public string? LastModified { get; init; }
}

/// <summary>
/// Session update notification.
/// </summary>
public record SessionNotification
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("update")]
    public required SessionUpdate Update { get; init; }
}

/// <summary>
/// Session update payload types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "sessionUpdate")]
[JsonDerivedType(typeof(AgentMessageChunk), "agent_message_chunk")]
[JsonDerivedType(typeof(ToolCallUpdate), "tool_call")]
[JsonDerivedType(typeof(ToolCallStatusUpdate), "tool_call_update")]
[JsonDerivedType(typeof(PlanUpdate), "plan")]
[JsonDerivedType(typeof(CurrentModeUpdate), "current_mode_update")]
public abstract record SessionUpdate;

public record AgentMessageChunk : SessionUpdate
{
    [JsonPropertyName("content")]
    public required ContentBlock Content { get; init; }
}

public record ToolCallUpdate : SessionUpdate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
    
    [JsonPropertyName("content")]
    public ContentBlock[]? Content { get; init; }
}

public record ToolCallStatusUpdate : SessionUpdate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("content")]
    public ContentBlock[]? Content { get; init; }
}

public record PlanUpdate : SessionUpdate
{
    [JsonPropertyName("entries")]
    public required PlanEntry[] Entries { get; init; }
}

public record PlanEntry
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }
}

public record CurrentModeUpdate : SessionUpdate
{
    [JsonPropertyName("currentModeId")]
    public required string CurrentModeId { get; init; }
}

/// <summary>
/// Tool call status values.
/// </summary>
public static class ToolCallStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Plan entry status values.
/// </summary>
public static class PlanEntryStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// Diff representing file modifications.
/// </summary>
public record Diff
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    
    [JsonPropertyName("oldText")]
    public string? OldText { get; init; }
    
    [JsonPropertyName("newText")]
    public required string NewText { get; init; }
}
