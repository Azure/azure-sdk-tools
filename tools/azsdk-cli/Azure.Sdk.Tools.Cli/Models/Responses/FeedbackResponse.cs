// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response class for user feedback collection operations.
/// </summary>
public class FeedbackResponse : CommandResponse
{
    /// <summary>
    /// Indicates whether feedback was submitted to telemetry.
    /// </summary>
    [JsonPropertyName("feedback_submitted")]
    public bool FeedbackSubmitted { get; set; }

    /// <summary>
    /// The session ID associated with the feedback.
    /// </summary>
    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; set; }

    /// <summary>
    /// User-provided feedback about the session.
    /// </summary>
    [JsonPropertyName("user_feedback")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserFeedback { get; set; }

    /// <summary>
    /// Agent-generated summary of the session.
    /// </summary>
    [JsonPropertyName("agent_summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentSummary { get; set; }

    /// <summary>
    /// User-reported issues or bugs encountered during the session.
    /// </summary>
    [JsonPropertyName("issues_reported")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? IssuesReported { get; set; }

    /// <summary>
    /// The repository context for the session.
    /// </summary>
    [JsonPropertyName("repository")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repository { get; set; }

    /// <summary>
    /// Message to display to the user.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    protected override string Format()
    {
        var output = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }

        if (FeedbackSubmitted)
        {
            output.AppendLine("\n✓ Feedback submitted successfully!");
            
            if (!string.IsNullOrEmpty(SessionId))
            {
                output.AppendLine($"Session ID: {SessionId}");
            }

            if (!string.IsNullOrEmpty(AgentSummary))
            {
                output.AppendLine($"\nAgent Summary:\n{AgentSummary}");
            }

            if (!string.IsNullOrEmpty(UserFeedback))
            {
                output.AppendLine($"\nYour Feedback:\n{UserFeedback}");
            }

            if (IssuesReported?.Count > 0)
            {
                output.AppendLine($"\nIssues Reported ({IssuesReported.Count}):");
                foreach (var issue in IssuesReported)
                {
                    output.AppendLine($"  • {issue}");
                }
            }

            if (!string.IsNullOrEmpty(Repository))
            {
                output.AppendLine($"\nRepository: {Repository}");
            }
        }
        else
        {
            output.AppendLine("Feedback not submitted.");
        }

        return output.ToString();
    }
}
