// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

/// <summary>
/// Top-level response for the azsdk_sla_status tool.
/// Contains aggregate SLA compliance metrics plus actionable lists of issues
/// that are approaching or have breached their SLA thresholds.
///
/// Three SLA metrics are tracked:
///   - FQR (First Question Response): time to first team comment on customer-reported issues (business days)
///   - Bug Resolution: time from bug creation to close (calendar days)
///   - Question Resolution: time from question creation to close/addressed (calendar days)
/// </summary>
public class SLAStatusResponse : CommandResponse
{
    /// <summary>Service label that was queried (e.g., "KeyVault", "Storage").</summary>
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    /// <summary>Specific repo queried, or null if all SDK repos were searched.</summary>
    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    /// <summary>How far back (in days) the query looked for issues.</summary>
    [JsonPropertyName("lookback_days")]
    public int LookbackDays { get; set; }

    /// <summary>Count of all open issues matching the service label in the lookback window.</summary>
    [JsonPropertyName("total_open_issues")]
    public int TotalOpenIssues { get; set; }

    /// <summary>Count of open issues that also have the "customer-reported" label.</summary>
    [JsonPropertyName("customer_reported_open")]
    public int CustomerReportedOpen { get; set; }

    /// <summary>FQR metric: measures time to first team member comment (business days).</summary>
    [JsonPropertyName("first_question_response")]
    public SLAMetricSummary FirstQuestionResponse { get; set; } = new();

    /// <summary>Bug resolution metric: measures time from creation to close (calendar days).</summary>
    [JsonPropertyName("bug_resolution")]
    public SLAMetricSummary BugResolution { get; set; } = new();

    /// <summary>Question resolution metric: measures time from creation to close/addressed (calendar days).</summary>
    [JsonPropertyName("question_resolution")]
    public SLAMetricSummary QuestionResolution { get; set; } = new();

    /// <summary>Issues within the approaching-window of breaching SLA. Null if none.</summary>
    [JsonPropertyName("approaching_breaches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SLAIssueDetail>? ApproachingBreaches { get; set; }

    /// <summary>Issues that have already exceeded their SLA threshold. Null if none.</summary>
    [JsonPropertyName("breached_issues")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SLAIssueDetail>? BreachedIssues { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        var repoDisplay = Repo ?? "all repos";
        sb.AppendLine($"SLA Status: {Service} — {repoDisplay} (last {LookbackDays} days)");
        sb.AppendLine();

        sb.AppendLine(FormatMetricLine(FirstQuestionResponse));
        sb.AppendLine(FormatMetricLine(BugResolution));
        sb.AppendLine(FormatMetricLine(QuestionResolution));

        if (ApproachingBreaches is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"⚠ Approaching ({ApproachingBreaches.Count})");
            foreach (var issue in ApproachingBreaches)
            {
                sb.AppendLine($"  #{issue.IssueNumber}  \"{issue.Title}\"  {FormatTimeRemaining(issue.TimeUntilBreachDays)}  → {issue.Assignee ?? "unassigned"}");
            }
        }

        if (BreachedIssues is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"🚨 Breached ({BreachedIssues.Count})");
            foreach (var issue in BreachedIssues)
            {
                sb.AppendLine($"  #{issue.IssueNumber}  \"{issue.Title}\"  {issue.SLAMetricType} {FormatOverdue(issue.TimeUntilBreachDays)}  → {issue.Assignee ?? "unassigned"}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMetricLine(SLAMetricSummary metric)
    {
        if (metric.TotalTracked == 0)
        {
            return $"  {metric.MetricName} ({metric.SLAThresholdDisplay}):  no issues";
        }

        return $"  {metric.MetricName} ({metric.SLAThresholdDisplay}):  {metric.CompliancePercent:F1}%  ({metric.WithinSLA}/{metric.TotalTracked})";
    }

    private static string FormatTimeRemaining(double? days)
    {
        if (days == null)
        {
            return "";
        }

        var d = days.Value;
        if (d >= 0)
        {
            return $"{d:F0}d remaining";
        }

        return $"{Math.Abs(d):F0}d overdue";
    }

    private static string FormatOverdue(double? days)
    {
        if (days == null)
        {
            return "";
        }

        return $"{Math.Abs(days.Value):F0}d overdue";
    }
}
