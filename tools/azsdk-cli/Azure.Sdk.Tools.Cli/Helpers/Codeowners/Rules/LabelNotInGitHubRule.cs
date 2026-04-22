// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-LBL-001: Detect Label work items that don't exist as GitHub repo labels
/// in the repos where they are referenced (via Label Owners and Packages).
/// Report only.
/// Prerequisite: IGitHubService.GetRepoLabels must be implemented.
/// </summary>
public class LabelNotInGitHubRule(
    IGitHubService githubService
) : IAuditRule
{
    public int Priority => 40;
    public string RuleId => "AUD-LBL-001";
    public string Description => "Label work item doesn't exist as a GitHub repo label";
    public bool CanFix => false;

    // Cache repo labels to avoid redundant API calls
    private readonly ConcurrentDictionary<string, HashSet<string>> _repoLabelCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        // Clear per-run cache to avoid stale data in long-lived processes (MCP server)
        _repoLabelCache.Clear();

        var violations = new List<AuditViolation>();

        // Build a map of label → repos where it's used
        var labelToRepos = BuildLabelToReposMap(context);

        foreach (var label in context.WorkItemData.Labels.Values)
        {
            if (!labelToRepos.TryGetValue(label.WorkItemId, out var repos) || repos.Count == 0)
            {
                // Label not referenced by any Label Owner or Package — skip
                continue;
            }

            var missingRepos = new List<string>();
            foreach (var repo in repos)
            {
                var repoLabels = await GetRepoLabels(repo, ct);
                if (!repoLabels.Contains(label.LabelName))
                {
                    missingRepos.Add(repo);
                }
            }

            if (missingRepos.Count > 0)
            {
                violations.Add(new AuditViolation
                {
                    RuleId = RuleId,
                    Description = $"Label '{label.LabelName}' ({label.WorkItemId}): not found in GitHub repos [{string.Join(", ", missingRepos)}]",
                    WorkItemId = label.WorkItemId,
                    Detail = $"Missing from: {string.Join(", ", missingRepos)}, Referenced in: {string.Join(", ", repos)}",
                });
            }
        }

        return violations;
    }

    public Task<List<AuditFixAction>> GetFixes(AuditContext context, List<AuditViolation> violations, CancellationToken ct)
    {
        throw new NotImplementedException($"{RuleId} is report-only and does not support fixes.");
    }

    /// <summary>
    /// Builds a mapping from Label WI ID to the set of repos where that label is referenced,
    /// derived from Label Owner and Package work items.
    /// </summary>
    private Dictionary<int, HashSet<string>> BuildLabelToReposMap(AuditContext context)
    {
        var map = new Dictionary<int, HashSet<string>>();

        // From Label Owners: use Custom.Repository directly
        foreach (var lo in context.WorkItemData.LabelOwners)
        {
            if (string.IsNullOrEmpty(lo.Repository))
            {
                continue;
            }

            foreach (var label in lo.Labels)
            {
                if (!map.TryGetValue(label.WorkItemId, out var repos))
                {
                    repos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[label.WorkItemId] = repos;
                }
                repos.Add(lo.Repository);
            }
        }

        // From Packages: map language → repo
        foreach (var pkg in context.WorkItemData.Packages.Values)
        {
            var repoName = SdkLanguageHelpers.GetRepoName(SdkLanguageHelpers.GetSdkLanguage(pkg.Language));
            if (string.IsNullOrEmpty(repoName))
            {
                continue;
            }
            var repo = $"Azure/{repoName}";

            foreach (var label in pkg.Labels)
            {
                if (!map.TryGetValue(label.WorkItemId, out var repos))
                {
                    repos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[label.WorkItemId] = repos;
                }
                repos.Add(repo);
            }
        }

        return map;
    }

    private async Task<HashSet<string>> GetRepoLabels(string repoFullName, CancellationToken ct)
    {
        if (_repoLabelCache.TryGetValue(repoFullName, out var cached))
        {
            return cached;
        }

        var parts = repoFullName.Split('/');
        string owner, repoName;
        if (parts.Length == 2)
        {
            owner = parts[0];
            repoName = parts[1];
        }
        else
        {
            owner = "Azure";
            repoName = repoFullName;
        }

        var labels = await githubService.GetRepoLabels(owner, repoName, ct);
        _repoLabelCache[repoFullName] = labels;
        return labels;
    }
}
