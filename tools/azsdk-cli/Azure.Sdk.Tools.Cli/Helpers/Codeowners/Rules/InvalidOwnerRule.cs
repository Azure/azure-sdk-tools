// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-OWN-001: Detect individual Owner work items that fail GitHub validation
/// (not in Azure or Microsoft orgs, no write access to azure-sdk-for-net).
/// Fix: Set "Invalid Since" field to current date/time on newly invalid owners.
///       Clear "Invalid Since" field on owners that have become valid again.
/// Safety threshold: throws if >5 newly invalid owners detected with --fix unless --force.
/// </summary>
public class InvalidOwnerRule(
    ICodeownersValidatorHelper validatorHelper,
    IDevOpsService devOpsService,
    ILogger<InvalidOwnerRule> logger
) : IAuditRule
{
    private const int SafetyThreshold = 5;
    public const string DoNothingDetail = "Do nothing";
    public const string SetInvalidDetail = "Set Invalid";
    public const string ClearInvalidDetail = "Clear Invalid";
    public static readonly string[] ValidDetails = [DoNothingDetail, SetInvalidDetail, ClearInvalidDetail];

    public int Priority => 10;
    public string RuleId => "AUD-OWN-001";
    public string Description => "Individual owner fails GitHub validation";
    public bool CanFix => true;

    public async Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();
        var individualOwners = context.WorkItemData.Owners.Values
            .Where(o => !o.IsGitHubTeam)
            .ToList();

        int count = 0;
        foreach (var owner in individualOwners)
        {
            count++;
            logger.LogInformation(
                "{count}/{total} Validating owner '{OwnerAlias}' ({WorkItemId})",
                count,
                individualOwners.Count,
                owner.GitHubAlias,
                owner.WorkItemId
            );
            var result = await validatorHelper.ValidateCodeOwnerAsync(owner.GitHubAlias, ct: ct);

            // If the validator returns an error status for a NotFoundException, treat as invalid
            if (result.Status == "Error" && result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Owner '{owner.GitHubAlias}' ({owner.WorkItemId}): GitHub user not found",
                    WorkItemId = owner.WorkItemId,
                    Detail = owner.InvalidSince.HasValue ? DoNothingDetail : SetInvalidDetail, // If user not found, only set Invalid Since if not already set
                });
                continue;
            }

            // Rate limit or other transient errors: propagate as exception to fail the audit
            if (result.Status == "Error")
            {
                throw new InvalidOperationException(
                    $"Validation error for owner '{owner.GitHubAlias}' ({owner.WorkItemId}): {result.Message}");
            }

            if (!result.IsValidCodeOwner)
            {
                var orgs = string.Join(", ", result.Organizations.Select(kv => $"{kv.Key}={kv.Value}"));
                var description = $"Owner '{owner.GitHubAlias}' ({owner.WorkItemId}): not a valid code owner (Organizations: {orgs}, HasWritePermission: {result.HasWritePermission})";

                logger.LogWarning(
                    "Owner '{alias}' ({WorkItemId}): not a valid code owner (Organizations: {orgs}, HasWritePermission: {hasWritePermission})",
                    owner.GitHubAlias,
                    owner.WorkItemId,
                    orgs,
                    result.HasWritePermission
                );

                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = description,
                    WorkItemId = owner.WorkItemId,
                    Detail = owner.InvalidSince.HasValue ? DoNothingDetail : SetInvalidDetail, // Only set Invalid Since if not already set
                });
            }
            else if (owner.InvalidSince.HasValue)
            {
                // Valid owner that was previously marked invalid — report for recovery
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Owner '{owner.GitHubAlias}' ({owner.WorkItemId}): now valid, clear Invalid Since",
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

        // Safety threshold applies only to newly invalid owners (not already marked, not recoveries)
        if (setInvalidViolations.Count > SafetyThreshold && !context.Force)
        {
            throw new InvalidOperationException(
                $"{RuleId}: {setInvalidViolations.Count} newly invalid owners detected (threshold is {SafetyThreshold}). " +
                $"Use --force to override. All invalid owners have been logged for review.");
        }

        var fixes = new List<AuditFixAction>();
        var now = DateTime.UtcNow;

        foreach (var violation in setInvalidViolations)
        {
            var ownerId = violation.WorkItemId!.Value;
            var owner = context.WorkItemData.Owners[ownerId].GitHubAlias!;
            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Set Invalid Since on Owner '{owner}' ({ownerId})",
                Apply = async (fixCt) => await SetInvalidSince(ownerId, owner, now, fixCt),
            });
        }

        foreach (var violation in clearInvalidViolations)
        {
            var ownerId = violation.WorkItemId!.Value;
            var owner = context.WorkItemData.Owners[ownerId].GitHubAlias!;
            fixes.Add(new AuditFixAction
            {
                RuleId = RuleId,
                Description = $"Clear Invalid Since on Owner '{owner}' ({ownerId})",
                Apply = async (fixCt) => await ClearInvalidSince(ownerId, owner, fixCt),
            });
        }

        return Task.FromResult(fixes);
    }

    private async Task<AuditFixResult> SetInvalidSince(int ownerId, string alias, DateTime invalidSince, CancellationToken ct)
    {
        var desc = $"Set Invalid Since on Owner '{alias}' ({ownerId})";
        await devOpsService.UpdateWorkItemAsync(ownerId, new Dictionary<string, string>
        {
            ["Custom.InvalidSince"] = invalidSince.ToString("o")
        }, ct);
        return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
    }

    private async Task<AuditFixResult> ClearInvalidSince(int ownerId, string alias, CancellationToken ct)
    {
        var desc = $"Clear Invalid Since on Owner '{alias}' ({ownerId})";
        await devOpsService.UpdateWorkItemAsync(ownerId, new Dictionary<string, string>
        {
            ["Custom.InvalidSince"] = ""
        }, ct);
        return new AuditFixResult { RuleId = RuleId, Description = desc, Success = true };
    }
}
