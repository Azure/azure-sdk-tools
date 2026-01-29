// Agent Client Protocol - .NET SDK
// Permission request types

using System.Text.Json.Serialization;

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Request permission from user.
/// </summary>
public record RequestPermissionRequest
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("options")]
    public required PermissionOption[] Options { get; init; }
}

/// <summary>
/// Permission option.
/// </summary>
public record PermissionOption(string Id, string Label, string Kind)
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Id;
    
    [JsonPropertyName("label")]
    public string Label { get; init; } = Label;
    
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = Kind;
}

/// <summary>
/// Response to permission request.
/// </summary>
public record RequestPermissionResponse
{
    [JsonPropertyName("_meta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Meta { get; init; }
    
    [JsonPropertyName("outcome")]
    public required PermissionOutcome Outcome { get; init; }
}

/// <summary>
/// Permission outcome.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "outcome")]
[JsonDerivedType(typeof(SelectedPermissionOutcome), "selected")]
[JsonDerivedType(typeof(DismissedPermissionOutcome), "dismissed")]
public abstract record PermissionOutcome;

public record SelectedPermissionOutcome : PermissionOutcome
{
    [JsonPropertyName("optionId")]
    public required string OptionId { get; init; }
}

public record DismissedPermissionOutcome : PermissionOutcome;

/// <summary>
/// Permission option kinds.
/// </summary>
public static class PermissionKind
{
    public const string AllowOnce = "allow_once";
    public const string AllowAlways = "allow_always";
    public const string RejectOnce = "reject_once";
    public const string RejectAlways = "reject_always";
}

/// <summary>
/// Request text input from user.
/// </summary>
public record RequestInputRequest
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }
    
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }
    
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
    
    [JsonPropertyName("defaultValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Response to input request.
/// </summary>
public record RequestInputResponse
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }
    
    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; init; }
}
