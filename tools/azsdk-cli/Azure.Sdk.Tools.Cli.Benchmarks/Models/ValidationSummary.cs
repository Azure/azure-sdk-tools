// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Summary of all validation results.
/// </summary>
public class ValidationSummary
{
    /// <summary>
    /// Gets the individual validation results.
    /// </summary>
    public required IReadOnlyList<ValidationResult> Results { get; init; }

    /// <summary>
    /// Gets the total duration of all validations.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets whether all validators passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets the count of passed validators.
    /// </summary>
    public int PassedCount { get; init; }

    /// <summary>
    /// Gets the count of failed validators.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets the failed validation results.
    /// </summary>
    public IEnumerable<ValidationResult> FailedResults => 
        Results.Where(r => !r.Passed);
}
