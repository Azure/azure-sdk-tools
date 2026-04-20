// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-OWN-001: Detect individual Owner work items that fail GitHub validation
/// (not in Azure/Microsoft orgs, no write access to azure-sdk-for-net).
/// Fix: Remove relations from all linked Label Owner and Package work items.
/// Safety threshold: throws if >5 invalid owners detected with --fix unless --force.
/// </summary>
public class InvalidOwnerRule(
    ICodeownersValidatorHelper validatorHelper,
    IDevOpsService devOpsService,
    ILogger<InvalidOwnerRule> logger
) : IAuditRule
{
    private const int SafetyThreshold = 5;

    public string RuleId => "AUD-OWN-001";
    public string Description => "Individual owner fails GitHub validation";
    public bool CanFix => true;

    public async Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();
        var individualOwners = context.WorkItemData.Owners.Values
            .Where(o => !o.IsGitHubTeam)
            .ToList();

        foreach (var owner in individualOwners)
        {
            var result = await validatorHelper.ValidateCodeOwnerAsync(owner.GitHubAlias, ct: ct);

            // If the validator returns an error status for a NotFoundException, treat as invalid
            if (result.Status == "Error" && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Owner '{owner.GitHubAlias}' (WI {owner.WorkItemId}): GitHub user not found",
                    WorkItemId = owner.WorkItemId,
                    WorkItemTitle = owner.Title,
                    Detail = result.Message,
                });
                continue;
            }

            // Rate limit or other transient errors: propagate as exception to fail the audit
            if (result.Status == "Error")
            {
                throw new InvalidOperationException(
                    $"Validation error for owner '{owner.GitHubAlias}' (WI {owner.WorkItemId}): {result.Message}");
            }

            if (!result.IsValidCodeOwner)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Owner '{owner.GitHubAlias}' (WI {owner.WorkItemId}): not a valid code owner",
                    WorkItemId = owner.WorkItemId,
                    WorkItemTitle = owner.Title,
                    Detail = result.Message,
                });
            }
        }

        // Log all invalid owners for human review
        if (violations.Count > 0)
        {
            logger.LogWarning("{RuleId}: Found {Count} invalid owner(s):", RuleId, violations.Count);
            foreach (var v in violations)
            {
                logger.LogWarning("  - {Description}", v.Description);
            }
        }

        return violations;
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        // Safety threshold: if more than 5 invalid owners, require --force
        if (violations.Count > SafetyThreshold && !context.Force)
        {
            throw new InvalidOperationException(
                $"{RuleId}: {violations.Count} invalid owners detected (threshold is {SafetyThreshold}). " +
                $"Use --force to override. All invalid owners have been logged above for review.");
        }

        var fixes = new List<AuditFixAction>();
        var invalidOwnerIds = violations
            .Where(v => v.WorkItemId.HasValue)
            .Select(v => v.WorkItemId!.Value)
            .ToHashSet();

        // Find all Label Owners and Packages that reference invalid owners
        foreach (var labelOwner in context.WorkItemData.LabelOwners)
        {
            foreach (var owner in labelOwner.Owners.Where(o => invalidOwnerIds.Contains(o.WorkItemId)))
            {
                var loId = labelOwner.WorkItemId;
                var ownerId = owner.WorkItemId;
                var ownerAlias = owner.GitHubAlias;
                fixes.Add(new AuditFixAction
                {
                    RuleId = RuleId,
                    Description = $"Remove relation: Label Owner {loId} → Owner '{ownerAlias}' ({ownerId})",
                    Apply = async (fixCt) => await RemoveRelationSafe(loId, ownerId, ownerAlias, fixCt),
                });
            }
        }

        foreach (var package in context.WorkItemData.Packages.Values)
        {
            foreach (var owner in package.Owners.Where(o => invalidOwnerIds.Contains(o.WorkItemId)))
            {
                var pkgId = package.WorkItemId;
                var ownerId = owner.WorkItemId;
                var ownerAlias = owner.GitHubAlias;
                fixes.Add(new AuditFixAction
                {
                    RuleId = RuleId,
                    Description = $"Remove relation: Package {pkgId} ({package.PackageName}) → Owner '{ownerAlias}' ({ownerId})",
                    Apply = async (fixCt) => await RemoveRelationSafe(pkgId, ownerId, ownerAlias, fixCt),
                });
            }
        }

        return Task.FromResult(fixes);
    }

    private async Task<AuditFixResult> RemoveRelationSafe(int sourceId, int targetId, string ownerAlias, CancellationToken ct)
    {
        var desc = $"Remove relation: {sourceId} → Owner '{ownerAlias}' ({targetId})";
        try
        {
            await devOpsService.RemoveWorkItemRelationAsync(sourceId, "Related", targetId, ct);
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                    ex.Message.Contains("no relations", StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent: relation was already removed
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true, AlreadyApplied = true };
        }
        catch (Exception ex) when (ex.Message.Contains("409") || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase))
        {
            // Conflict: work item was modified concurrently; retry once
            logger.LogWarning("409 conflict removing relation {Source} → {Target}, retrying", sourceId, targetId);
            try
            {
                await devOpsService.RemoveWorkItemRelationAsync(sourceId, "Related", targetId, ct);
                return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
            }
            catch (Exception retryEx) when (retryEx.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                             retryEx.Message.Contains("no relations", StringComparison.OrdinalIgnoreCase))
            {
                return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true, AlreadyApplied = true };
            }
        }
    }
}
