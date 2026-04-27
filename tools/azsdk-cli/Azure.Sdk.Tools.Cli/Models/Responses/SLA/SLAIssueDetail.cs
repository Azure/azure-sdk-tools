// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

/// <summary>
/// Detail for a single issue that is approaching or has breached its SLA.
/// Used in the ApproachingBreaches and BreachedIssues lists of <see cref="SLAStatusResponse"/>.
/// </summary>
public class SLAIssueDetail
{
    /// <summary>Full GitHub URL to the issue.</summary>
    [JsonPropertyName("issue_url")]
    public string IssueUrl { get; set; } = string.Empty;

    /// <summary>GitHub issue number.</summary>
    [JsonPropertyName("issue_number")]
    public int IssueNumber { get; set; }

    /// <summary>Issue title (truncated to 80 chars).</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Repository name (e.g., "azure-sdk-for-python").</summary>
    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    /// <summary>GitHub login of the assigned user, or null if unassigned.</summary>
    [JsonPropertyName("assignee")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Assignee { get; set; }

    /// <summary>When the issue was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>"approaching" if within the warning window, "breached" if past the SLA threshold.</summary>
    [JsonPropertyName("sla_status")]
    public string SLAStatus { get; set; } = string.Empty;

    /// <summary>
    /// Days until SLA breach (positive = time remaining, negative = days overdue).
    /// For FQR this is in business days; for resolution metrics it's calendar days.
    /// </summary>
    [JsonPropertyName("time_until_breach_days")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TimeUntilBreachDays { get; set; }

    /// <summary>Which SLA metric this issue is tracked under: "fqr", "bug_resolution", or "question_resolution".</summary>
    [JsonPropertyName("sla_metric_type")]
    public string SLAMetricType { get; set; } = string.Empty;
}
