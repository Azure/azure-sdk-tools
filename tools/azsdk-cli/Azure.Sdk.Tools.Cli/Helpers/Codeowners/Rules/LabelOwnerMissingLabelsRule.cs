// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-STR-002: Detect Label Owner work items with zero Label relations.
/// Depends on: AUD-LBL-001, AUD-LBL-002 (label changes may leave Label Owners without labels).
/// Report only — no fix, requires human investigation for data integrity issues.
/// </summary>
public class LabelOwnerMissingLabelsRule : IAuditRule
{
    public int Priority => 70;
    public string RuleId => "AUD-STR-002";
    public string Description => "Label Owner has zero label relations";
    public bool CanFix => false;

    public Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();

        foreach (var lo in context.WorkItemData.LabelOwners)
        {
            if (lo.Labels.Count == 0)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Label Owner {lo.WorkItemId}: has zero labels",
                    WorkItemId = lo.WorkItemId,
                    Detail = $"Type: {lo.LabelType}, Repository: {lo.Repository}, Path: {lo.RepoPath}, Owners: {lo.Owners.Count}",
                });
            }
        }

        return Task.FromResult(violations);
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        throw new NotImplementedException($"{RuleId} is report-only and does not support fixes.");
    }
}
