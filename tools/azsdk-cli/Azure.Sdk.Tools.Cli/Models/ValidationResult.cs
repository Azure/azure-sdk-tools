// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Result of validation operations for update language services
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether the validation was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// List of validation errors encountered during the validation process
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Optional warnings that don't prevent success but should be noted
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Additional metadata about the validation process
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult CreateSuccess(List<string>? warnings = null)
    {
        return new ValidationResult
        {
            Success = true,
            Warnings = warnings ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a failed validation result with errors
    /// </summary>
    public static ValidationResult CreateFailure(List<string> errors, List<string>? warnings = null)
    {
        return new ValidationResult
        {
            Success = false,
            Errors = errors,
            Warnings = warnings ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a failed validation result with a single error
    /// </summary>
    public static ValidationResult CreateFailure(string error, List<string>? warnings = null)
    {
        return CreateFailure(new List<string> { error }, warnings);
    }
}
