// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

public class SLAStatusResponse : CommandResponse
{
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("lookback_days")]
    public int LookbackDays { get; set; }

    [JsonPropertyName("total_open_issues")]
    public int TotalOpenIssues { get; set; }

    [JsonPropertyName("customer_reported_open")]
    public int CustomerReportedOpen { get; set; }

    [JsonPropertyName("first_question_response")]
    public SLAMetricSummary FirstQuestionResponse { get; set; } = new();

    [JsonPropertyName("bug_resolution")]
    public SLAMetricSummary BugResolution { get; set; } = new();

    [JsonPropertyName("question_resolution")]
    public SLAMetricSummary QuestionResolution { get; set; } = new();

    [JsonPropertyName("approaching_breaches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SLAIssueDetail>? ApproachingBreaches { get; set; }

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
