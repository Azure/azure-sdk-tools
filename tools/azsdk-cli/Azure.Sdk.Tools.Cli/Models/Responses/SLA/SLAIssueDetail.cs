// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

public class SLAIssueDetail
{
    [JsonPropertyName("issue_url")]
    public string IssueUrl { get; set; } = string.Empty;

    [JsonPropertyName("issue_number")]
    public int IssueNumber { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    [JsonPropertyName("assignee")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Assignee { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("sla_status")]
    public string SLAStatus { get; set; } = string.Empty;

    [JsonPropertyName("time_until_breach_days")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TimeUntilBreachDays { get; set; }

    [JsonPropertyName("sla_metric_type")]
    public string SLAMetricType { get; set; } = string.Empty;
}
