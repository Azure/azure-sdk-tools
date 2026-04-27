// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-OWN-003: Detect team Owner work items that don't descend from azure-sdk-write.
/// Skips malformed aliases internally (AUD-OWN-002 is report-only, can't rely on it fixing them).
/// Fix: Set "Invalid Since" field to current date/time on newly invalid teams.
///       Clear "Invalid Since" field on teams that have become valid again.
/// Safety threshold: throws if >5 newly invalid teams detected with --fix unless --force.
/// </summary>
public class TeamNotWriteRule(
    ITeamUserCache teamUserCache,
    IGitHubService githubService,
    IDevOpsService devOpsService
) : IAuditRule
{
    private const int SafetyThreshold = 5;
    public const string DoNothingDetail = "Do nothing";
    public const string SetInvalidDetail = "Set Invalid";
    public const string ClearInvalidDetail = "Clear Invalid";
    public static readonly string[] ValidDetails = [DoNothingDetail, SetInvalidDetail, ClearInvalidDetail];

    public int Priority => 30;
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
                    Description = $"Team '{owner.GitHubAlias}' ({owner.WorkItemId}): not a descendant of azure-sdk-write",
                    WorkItemId = owner.WorkItemId,
                    Detail = owner.InvalidSince.HasValue ? DoNothingDetail : SetInvalidDetail,
                });
            }
            else if (owner.InvalidSince.HasValue)
            {
                // Valid team that was previously marked invalid — report for recovery
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Team '{owner.GitHubAlias}' ({owner.WorkItemId}): now valid, clear Invalid Since",
                    WorkItemId = owner.WorkItemId,
                    Detail = ClearInvalidDetail,
                });
            }
        }

        return violations;
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        var clearInvalidViolations = violations.Where(v => v.Detail == ClearInvalidDetail).ToList();
        var setInvalidViolations = violations.Where(v => v.Detail == SetInvalidDetail).ToList();

        if (violations.Any(v => !ValidDetails.Contains(v.Detail)))
        {
            throw new InvalidOperationException($"Unexpected violation detail value detected in {RuleId} fixes: {string.Join(", ", violations.Select(v => v.Detail))}");
        }

        // Safety threshold applies only to newly invalid teams (not already marked, not recoveries)
        if (setInvalidViolations.Count > SafetyThreshold && !context.Force)
        {
            throw new InvalidOperationException(
                $"{RuleId}: {setInvalidViolations.Count} newly invalid teams detected (threshold is {SafetyThreshold}). " +
                $"Use --force to override. All invalid teams have been logged for review.");
        }

        var fixes = new List<AuditFixAction>();
        var now = DateTime.UtcNow;

        foreach (var violation in setInvalidViolations)
        {
            var ownerId = violation.WorkItemId!.Value;
            var alias = context.WorkItemData.Owners[ownerId].GitHubAlias;
            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Set Invalid Since on Team '{alias}' ({ownerId})",
                Apply = async (fixCt) => await SetInvalidSince(ownerId, alias, now, fixCt),
            });
        }

        foreach (var violation in clearInvalidViolations)
        {
            var ownerId = violation.WorkItemId!.Value;
            var alias = context.WorkItemData.Owners[ownerId].GitHubAlias;
            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Clear Invalid Since on Team '{alias}' ({ownerId})",
                Apply = async (fixCt) => await ClearInvalidSince(ownerId, alias, fixCt),
            });
        }

        return Task.FromResult(fixes);
    }

    private async Task<bool> CheckTeamIsUnderWriteTeam(string teamSlug, CancellationToken ct)
    {
        // azure-sdk-write itself is valid
        if (teamSlug.Equals("azure-sdk-write", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check the azure-sdk-write team cache (contains all descendant teams)
        if (teamUserCache.TeamUserDict.ContainsKey(teamSlug))
        {
            return true;
        }

        // Fall back to GitHub API parent-chain check
        try
        {
            var current = await githubService.GetTeamByNameAsync("Azure", teamSlug, ct);
            while (current?.Parent != null)
            {
                if (current.Parent.Slug.Equals("azure-sdk-write", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                current = await githubService.GetTeamByNameAsync("Azure", current.Parent.Slug, ct);
            }
            return false;
        }
        catch (Octokit.NotFoundException)
        {
            return false;
        }
    }

    private async Task<AuditFixResult> SetInvalidSince(int ownerId, string alias, DateTime invalidSince, CancellationToken ct)
    {
        var desc = $"Set Invalid Since on Team '{alias}' ({ownerId})";
        await devOpsService.UpdateWorkItemAsync(ownerId, new Dictionary<string, string>
        {
            ["Custom.InvalidSince"] = invalidSince.ToString("o")
        }, ct);
        return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
    }

    private async Task<AuditFixResult> ClearInvalidSince(int ownerId, string alias, CancellationToken ct)
    {
        var desc = $"Clear Invalid Since on Team '{alias}' ({ownerId})";
        await devOpsService.UpdateWorkItemAsync(ownerId, new Dictionary<string, string>
        {
            ["Custom.InvalidSince"] = ""
        }, ct);
        return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
    }
}
