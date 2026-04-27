// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

/// <summary>
/// Compliance summary for a single SLA metric (FQR, Bug Resolution, or Question Resolution).
/// Breaks down tracked issues into within-SLA, approaching, and breached buckets.
/// </summary>
public class SLAMetricSummary
{
    /// <summary>Human-readable metric name (e.g., "FQR", "Bug Resolution").</summary>
    [JsonPropertyName("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>SLA threshold in days (business days for FQR, calendar days for resolution metrics).</summary>
    [JsonPropertyName("sla_threshold_days")]
    public int SLAThresholdDays { get; set; }

    /// <summary>Display string for the threshold (e.g., "3bd" for business days, "90d" for calendar days).</summary>
    [JsonPropertyName("sla_threshold_display")]
    public string SLAThresholdDisplay { get; set; } = string.Empty;

    /// <summary>Total number of issues evaluated for this metric.</summary>
    [JsonPropertyName("total_tracked")]
    public int TotalTracked { get; set; }

    /// <summary>Issues that were responded to / resolved within the SLA threshold.</summary>
    [JsonPropertyName("within_sla")]
    public int WithinSLA { get; set; }

    /// <summary>Open issues within the approaching-window of breaching (still within SLA).</summary>
    [JsonPropertyName("approaching")]
    public int Approaching { get; set; }

    /// <summary>Issues that have exceeded the SLA threshold.</summary>
    [JsonPropertyName("breached")]
    public int Breached { get; set; }

    /// <summary>Percentage of tracked issues that are within SLA (WithinSLA / TotalTracked * 100).</summary>
    [JsonPropertyName("compliance_percent")]
    public double CompliancePercent { get; set; }
}
