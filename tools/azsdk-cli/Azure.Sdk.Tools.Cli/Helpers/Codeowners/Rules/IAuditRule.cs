// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// Interface for audit rules that evaluate work item data and optionally produce fixes.
/// </summary>
public interface IAuditRule
{
    int Priority { get; }
    string RuleId { get; }
    string Description { get; }
    bool CanFix { get; }
    Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct);
    Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct);
}
