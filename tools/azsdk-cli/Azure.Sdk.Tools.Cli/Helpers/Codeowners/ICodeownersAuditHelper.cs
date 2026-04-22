// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public interface ICodeownersAuditHelper
{
    Task<AuditReport> RunAudit(bool fix, bool force, string? repo, CancellationToken ct);
}

/// <summary>
/// Final report produced by the audit.
/// </summary>
public class AuditReport
{
    public List<AuditViolation> Violations { get; set; } = [];
    public List<AuditFixResult> FixesApplied { get; set; } = [];
}
