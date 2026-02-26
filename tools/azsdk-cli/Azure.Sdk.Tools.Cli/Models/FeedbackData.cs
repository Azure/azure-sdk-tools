// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Status of a feedback item during classification
/// </summary>
public enum FeedbackStatus
{
    /// <summary>
    /// Item is still being worked on and can be customized via TypeSpec
    /// </summary>
    TSP_APPLICABLE,
    
    /// <summary>
    /// Item has been successfully resolved
    /// </summary>
    SUCCESS,
    
    /// <summary>
    /// Item cannot be resolved and requires manual intervention
    /// </summary>
    REQUIRES_MANUAL_INTERVENTION
}

/// <summary>
/// Individual feedback item for classification
/// </summary>
public class FeedbackItem
{
    /// <summary>
    /// Unique identifier for the feedback item (8-char short GUID).
    /// Kept short to reduce LLM transcription errors while remaining unique within a batch.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
    
    /// <summary>
    /// The feedback/error text
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of this feedback item
    /// </summary>
    public FeedbackStatus Status { get; set; } = FeedbackStatus.TSP_APPLICABLE;
    
    /// <summary>
    /// Running context of changes applied for this item (newline-separated entries)
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for the classification decision
    /// </summary>
    public string ClassificationReason { get; set; } = string.Empty;

    /// <summary>
    /// Appends content to the running context. If section is provided, wraps content in labeled section markers.
    /// </summary>
    public void AppendContext(string content, string? section = null, int leadingNewLines = 0)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var sb = new StringBuilder(Context);
        for (var i = 0; i < leadingNewLines; i++)
        {
            sb.Append('\n');
        }

        if (string.IsNullOrWhiteSpace(section))
        {
            sb.Append(content);
            Context = sb.ToString();
            return;
        }

        var formattedBuilder = new StringBuilder();
        formattedBuilder
            .Append("=== ")
            .Append(section)
            .Append(" ===\n")
            .Append(content)
            .Append("\n=== End ")
            .Append(section)
            .Append(" ===");

        var formatted = formattedBuilder.ToString();

        if (sb.Length > 0)
        {
            sb.Append("\n\n");
        }

        sb.Append(formatted);
        Context = sb.ToString();
    }
}
