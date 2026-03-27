// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.SLA;

public class SLAMetricSummary
{
    [JsonPropertyName("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("sla_threshold_days")]
    public int SLAThresholdDays { get; set; }

    [JsonPropertyName("sla_threshold_display")]
    public string SLAThresholdDisplay { get; set; } = string.Empty;

    [JsonPropertyName("total_tracked")]
    public int TotalTracked { get; set; }

    [JsonPropertyName("within_sla")]
    public int WithinSLA { get; set; }

    [JsonPropertyName("approaching")]
    public int Approaching { get; set; }

    [JsonPropertyName("breached")]
    public int Breached { get; set; }

    [JsonPropertyName("compliance_percent")]
    public double CompliancePercent { get; set; }
}
