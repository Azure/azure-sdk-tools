// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-STR-001: Detect Label Owner work items with zero Owner relations.
/// Depends on: AUD-OWN-001, AUD-OWN-003 (owner removals may leave Label Owners orphaned).
/// Fix: Delete the Label Owner work item.
/// </summary>
public class LabelOwnerMissingOwnersRule(
    IDevOpsService devOpsService,
    ILogger<LabelOwnerMissingOwnersRule> logger
) : IAuditRule
{
    public string RuleId => "AUD-STR-001";
    public string Description => "Label Owner has zero owner relations";
    public bool CanFix => true;

    public Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();

        foreach (var lo in context.WorkItemData.LabelOwners)
        {
            if (lo.Owners.Count == 0)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Label Owner {lo.WorkItemId}: has zero owners",
                    WorkItemId = lo.WorkItemId,
                    WorkItemTitle = lo.Title,
                    Detail = $"Type: {lo.LabelType}, Repository: {lo.Repository}, Path: {lo.RepoPath}, Labels: {lo.Labels.Count}",
                });
            }
        }

        return Task.FromResult(violations);
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        var fixes = new List<AuditFixAction>();

        foreach (var violation in violations)
        {
            if (!violation.WorkItemId.HasValue)
            {
                continue;
            }
            var wiId = violation.WorkItemId.Value;

            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Delete Label Owner work item {wiId}",
                Apply = async (fixCt) => await DeleteWorkItemSafe(wiId, fixCt),
            });
        }

        return Task.FromResult(fixes);
    }

    private async Task<AuditFixResult> DeleteWorkItemSafe(int workItemId, CancellationToken ct)
    {
        var desc = $"Delete Label Owner work item {workItemId}";
        try
        {
            await devOpsService.DeleteWorkItemAsync(workItemId, ct);
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
        }
        catch (Exception ex) when (ex.Message.Contains("404") ||
                                    ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent: already deleted
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true, AlreadyApplied = true };
        }
    }
}
