// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Holds all state needed by audit rules during evaluation.
/// </summary>
public class AuditContext
{
    public required WorkItemData WorkItemData { get; set; }
    public bool Fix { get; set; }
    public bool Force { get; set; }
    public string? Repo { get; set; }
}

/// <summary>
/// A single violation detected by an audit rule.
/// </summary>
public class AuditViolation
{
    [JsonPropertyName("rule_id")]
    public required string RuleId { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("work_item_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WorkItemId { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}

/// <summary>
/// A fix action that the harness should apply.
/// </summary>
public class AuditFixAction
{
    public required string RuleId { get; set; }
    public required string Description { get; set; }
    public required Func<CancellationToken, Task<AuditFixResult>> Apply { get; init; }
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
