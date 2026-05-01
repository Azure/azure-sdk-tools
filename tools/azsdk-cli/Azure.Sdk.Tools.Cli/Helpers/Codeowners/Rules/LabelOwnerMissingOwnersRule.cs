// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-STR-001: Detect Label Owner work items with zero Owner relations.
/// Depends on: AUD-OWN-001, AUD-OWN-003 (owner removals may leave Label Owners orphaned).
/// Fix: Delete the Label Owner work item.
/// Safety threshold: throws if >5 deletions with --fix unless --force.
/// </summary>
public class LabelOwnerMissingOwnersRule(
    IDevOpsService devOpsService,
    ILogger<LabelOwnerMissingOwnersRule> logger
) : IAuditRule
{
    private const int SafetyThreshold = 5;

    public int Priority => 60;
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
                    Detail = $"Type: {lo.LabelType}, Repository: {lo.Repository}, Path: {lo.RepoPath}, Labels: {lo.Labels.Count}",
                });
            }
        }

        // Log all zero-owner Label Owners for human review
        if (violations.Count > 0)
        {
            logger.LogWarning("{RuleId}: Found {Count} Label Owner(s) with zero owners:", RuleId, violations.Count);
            foreach (var v in violations)
            {
                logger.LogWarning("  - {Description} ({Detail})", v.Description, v.Detail);
            }
        }

        return Task.FromResult(violations);
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        // Safety threshold: if more than 5 deletions, require --force
        if (violations.Count > SafetyThreshold && !context.Force)
        {
            throw new InvalidOperationException(
                $"{RuleId}: {violations.Count} Label Owner deletions pending (threshold is {SafetyThreshold}). " +
                $"Use --force to override. All zero-owner Label Owners have been logged above for review.");
        }

        var fixes = new List<AuditFixAction>();

        foreach (var violation in violations)
        {
            if (!violation.WorkItemId.HasValue)
            {
                throw new InvalidOperationException($"Violation {violation.Description} does not have a WorkItemId, cannot apply fix.");
            }
            var wiId = violation.WorkItemId.Value;

            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Delete Label Owner work item {wiId}",
                Apply = async (fixCt) => await DeleteWorkItem(wiId, fixCt),
            });
        }

        return Task.FromResult(fixes);
    }

    private async Task<AuditFixResult> DeleteWorkItem(int workItemId, CancellationToken ct)
    {
        var desc = $"Delete Label Owner work item {workItemId}";
        await devOpsService.DeleteWorkItemAsync(workItemId, ct);
        return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
    }
}
