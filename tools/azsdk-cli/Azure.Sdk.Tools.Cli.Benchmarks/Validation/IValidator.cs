// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation;

/// <summary>
/// Defines a validator that checks whether a benchmark scenario passed or failed.
/// </summary>
public interface IValidator
{
    /// <summary>
    /// Gets the human-readable name of this validator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates the scenario result against specific criteria.
    /// </summary>
    /// <param name="context">The validation context containing execution results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(
        ValidationContext context, 
        CancellationToken cancellationToken = default);
}
