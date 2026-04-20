// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-OWN-003: Detect team Owner work items that don't descend from azure-sdk-write.
/// Skips malformed aliases internally (OWN-002 is report-only, can't rely on it fixing them).
/// Fix: Remove relations from all linked Label Owner and Package work items.
/// </summary>
public class TeamNotWriteRule(
    ITeamUserCache teamUserCache,
    IGitHubService githubService,
    IDevOpsService devOpsService,
    ILogger<TeamNotWriteRule> logger
) : IAuditRule
{
    public string RuleId => "AUD-OWN-003";
    public string Description => "Team doesn't descend from azure-sdk-write";
    public bool CanFix => true;

    public async Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();
        var teamOwners = context.WorkItemData.Owners.Values
            .Where(o => o.IsGitHubTeam)
            .ToList();

        foreach (var owner in teamOwners)
        {
            // Skip malformed aliases — those are handled by AUD-OWN-002
            var parts = owner.GitHubAlias.Split('/');
            if (parts.Length != 2 ||
                !parts[0].Equals("Azure", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                continue;
            }

            var teamSlug = parts[1];
            var isWriteTeam = await CheckTeamIsUnderWriteTeam(teamSlug, ct);

            if (!isWriteTeam)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Team '{owner.GitHubAlias}' (WI {owner.WorkItemId}): not a descendant of azure-sdk-write",
                    WorkItemId = owner.WorkItemId,
                    WorkItemTitle = owner.Title,
                });
            }
        }

        return violations;
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        var fixes = new List<AuditFixAction>();
        var invalidTeamIds = violations
            .Where(v => v.WorkItemId.HasValue)
            .Select(v => v.WorkItemId!.Value)
            .ToHashSet();

        foreach (var labelOwner in context.WorkItemData.LabelOwners)
        {
            foreach (var owner in labelOwner.Owners.Where(o => invalidTeamIds.Contains(o.WorkItemId)))
            {
                var loId = labelOwner.WorkItemId;
                var ownerId = owner.WorkItemId;
                var alias = owner.GitHubAlias;
                fixes.Add(new AuditFixAction
                {
                    RuleId = RuleId,
                    Description = $"Remove relation: Label Owner {loId} → Team '{alias}' ({ownerId})",
                    Apply = async (fixCt) => await RemoveRelationSafe(loId, ownerId, alias, fixCt),
                });
            }
        }

        foreach (var package in context.WorkItemData.Packages.Values)
        {
            foreach (var owner in package.Owners.Where(o => invalidTeamIds.Contains(o.WorkItemId)))
            {
                var pkgId = package.WorkItemId;
                var ownerId = owner.WorkItemId;
                var alias = owner.GitHubAlias;
                fixes.Add(new AuditFixAction
                {
                    RuleId = RuleId,
                    Description = $"Remove relation: Package {pkgId} ({package.PackageName}) → Team '{alias}' ({ownerId})",
                    Apply = async (fixCt) => await RemoveRelationSafe(pkgId, ownerId, alias, fixCt),
                });
            }
        }

        return Task.FromResult(fixes);
    }

    private async Task<bool> CheckTeamIsUnderWriteTeam(string teamSlug, CancellationToken ct)
    {
        // First check the azure-sdk-write team cache (contains all teams under azure-sdk-write)
        if (teamUserCache.TeamUserDict.ContainsKey(teamSlug))
        {
            return true;
        }

        // Fall back to GitHub API parent-chain check
        try
        {
            var team = await githubService.GetTeamByNameAsync("Azure", teamSlug, ct);
            if (team?.Parent != null)
            {
                // Walk parent chain to look for azure-sdk-write
                var current = team;
                while (current?.Parent != null)
                {
                    if (current.Parent.Slug.Equals("azure-sdk-write", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    current = await githubService.GetTeamByNameAsync("Azure", current.Parent.Slug, ct);
                }
            }
            return false;
        }
        catch (Octokit.NotFoundException)
        {
            return false;
        }
    }

    private async Task<AuditFixResult> RemoveRelationSafe(int sourceId, int targetId, string alias, CancellationToken ct)
    {
        var desc = $"Remove relation: {sourceId} → Team '{alias}' ({targetId})";
        try
        {
            await devOpsService.RemoveWorkItemRelationAsync(sourceId, "Related", targetId, ct);
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
        }
        catch (Exception ex) when (ex.Message.Contains("Relation of type", StringComparison.OrdinalIgnoreCase) &&
                                    ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Idempotent: the specific relation was already removed
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true, AlreadyApplied = true };
        }
        catch (Exception ex) when (ex.Message.Contains("has no relations", StringComparison.OrdinalIgnoreCase))
        {
            return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true, AlreadyApplied = true };
        }
    }
}
