// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-OWN-002: Detect Owner work items with team aliases that don't match Azure/team-name format.
/// Report only — no automated fix.
/// </summary>
public class MalformedTeamRule : IAuditRule
{
    public int Priority => 20;
    public string RuleId => "AUD-OWN-002";
    public string Description => "Team alias doesn't match Azure/<team> format";
    public bool CanFix => false;

    public Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        var violations = new List<AuditViolation>();
        var teamOwners = context.WorkItemData.Owners.Values
            .Where(o => o.IsGitHubTeam)
            .ToList();

        foreach (var owner in teamOwners)
        {
            // Valid format: Azure/<team-slug> (case-insensitive org check)
            var parts = owner.GitHubAlias.Split('/');
            if (parts.Length != 2 ||
                !parts[0].Equals("Azure", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Team owner '{owner.GitHubAlias}' ({owner.WorkItemId}): malformed alias, expected Azure/<team>",
                    WorkItemId = owner.WorkItemId,
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
