// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;

/// <summary>
/// Represents a source of feedback for SDK customization (APIView comments, build errors, etc.)
/// </summary>
public interface IFeedbackInput
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
/// Individual feedback item for classification
/// </summary>
public class FeedbackItem
{
    /// <summary>
    /// Unique identifier for tracking (e.g., LineNo, ErrorCode)
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Context information (e.g., line text, code snippet)
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// The actual feedback comment/message
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Formatted version for prompt
    /// </summary>
    public string FormattedForPrompt { get; set; } = string.Empty;
}
