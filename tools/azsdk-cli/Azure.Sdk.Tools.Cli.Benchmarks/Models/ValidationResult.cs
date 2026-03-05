// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// The result of running a single validator.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets the name of the validator that produced this result.
    /// </summary>
    public required string ValidatorName { get; init; }

    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets a human-readable message describing the result.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets additional details (command output, diff, etc.).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the duration of the validation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a passing result.
    /// </summary>
    public static ValidationResult Pass(string validatorName, string? message = null) =>
        new() { ValidatorName = validatorName, Passed = true, Message = message };

    /// <summary>
    /// Creates a failing result.
    /// </summary>
    public static ValidationResult Fail(string validatorName, string message, string? details = null) =>
        new() { ValidatorName = validatorName, Passed = false, Message = message, Details = details };
}
