// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Represents a source of feedback for SDK customization (APIView comments, build errors, etc.)
/// </summary>
public interface IFeedbackItem
{
    /// <summary>
    /// Preprocesses the input and returns a standardized feedback context
    /// </summary>
    Task<FeedbackContext> PreprocessAsync(CancellationToken ct = default);
}

/// <summary>
/// Standardized feedback context after preprocessing
/// </summary>
public class FeedbackContext
{
    /// <summary>
    /// Formatted feedback ready for classification prompt
    /// </summary>
    public string FormattedFeedback { get; set; } = string.Empty;
    
    /// <summary>
    /// Individual feedback items for per-item classification
    /// </summary>
    public List<FeedbackItem> FeedbackItems { get; set; } = new();
    
    /// <summary>
    /// Target SDK language (e.g., python, csharp, java)
    /// </summary>
    public string? Language { get; set; }
    
    /// <summary>
    /// Service name for the SDK
    /// </summary>
    public string? ServiceName { get; set; }
    
    /// <summary>
    /// Package name
    /// </summary>
    public string? PackageName { get; set; }
    
    /// <summary>
    /// Type of input source
    /// </summary>
    public string InputType { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional metadata specific to the input type
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Status of a feedback item during classification
/// </summary>
public enum FeedbackStatus
{
    /// <summary>
    /// Item is still being worked on and can be customized
    /// </summary>
    CUSTOMIZABLE,
    
    /// <summary>
    /// Item has been successfully resolved
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Item cannot be resolved and requires manual intervention
    /// </summary>
    FAILURE
}

/// <summary>
/// Individual feedback item for classification
/// </summary>
public class FeedbackItem
{
    /// <summary>
    /// Unique identifier for the feedback item (auto-generated UUID).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The feedback/error text
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Formatted version for prompt with Id, Text, and Context
    /// </summary>
    public string FormattedPrompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of this feedback item
    /// </summary>
    public FeedbackStatus Status { get; set; } = FeedbackStatus.CUSTOMIZABLE;
    
    /// <summary>
    /// Running context of changes applied for this item (newline-separated entries)
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// Next action guidance from the classifier (what should be done next)
    /// </summary>
    public string NextAction { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for the classification decision
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional metadata specific to this feedback item
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
