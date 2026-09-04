// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// A single problem found by a lint rule.
/// </summary>
public class LintViolation
{
    [JsonPropertyName("rule_id")]
    public required string RuleId { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    /// <summary>
    /// Repo-relative path, usually with a line number, of the ownership YAML that declares the
    /// offending entry.
    /// </summary>
    [JsonPropertyName("source_file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}
