// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// A single CI check reported on a GitHub commit. Covers both of the shapes GitHub exposes: modern
/// check runs (GitHub Actions, Azure Pipelines) and legacy commit statuses, normalized into one type
/// so callers do not have to care which API produced the result.
/// </summary>
public class PrCheckRun
{
    /// <summary>
    /// Conclusions that mean a check did not pass. Anything else - success, skipped, neutral, or a check
    /// that is still running and has no conclusion yet - is not a failure to report. Compared case-insensitively
    /// because the REST and GraphQL APIs disagree on the casing they return.
    /// </summary>
    private static readonly HashSet<string> FailedConclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "FAILURE", "ERROR", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE",
    };

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    [JsonPropertyName("details_url")]
    public string? DetailsUrl { get; set; }

    [JsonPropertyName("app_name")]
    public string? AppName { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// True when this check's conclusion means it did not pass. See <see cref="FailedConclusions"/> for the
    /// exact set. A null conclusion (still running) is not treated as a failure.
    /// </summary>
    [JsonIgnore]
    public bool IsFailed => Conclusion != null && FailedConclusions.Contains(Conclusion);
}
