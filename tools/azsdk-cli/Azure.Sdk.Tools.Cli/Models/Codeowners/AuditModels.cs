// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// A single violation detected by an audit rule.
/// </summary>
public class AuditViolation
{
    [JsonPropertyName("rule_id")]
    public required string RuleId { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Repo-relative path of the owners YAML file that declares the offending entry.
    /// </summary>
    [JsonPropertyName("source_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}

/// <summary>
/// Result of applying a single fix action.
/// </summary>
public class AuditFixResult
{
    [JsonPropertyName("rule_id")]
    public required string RuleId { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}
