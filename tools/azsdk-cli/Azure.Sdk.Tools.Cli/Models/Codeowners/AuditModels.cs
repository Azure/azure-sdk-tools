// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    public required string RuleId { get; set; }
    public required string Description { get; set; }
    public int? WorkItemId { get; set; }
    public string? WorkItemTitle { get; set; }
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
    public required string RuleId { get; set; }
    public required string Description { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
