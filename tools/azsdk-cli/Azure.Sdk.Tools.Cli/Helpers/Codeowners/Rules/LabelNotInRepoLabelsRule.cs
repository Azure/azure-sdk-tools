// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners.Rules;

/// <summary>
/// AUD-LBL-001: Detect Label work items that don't exist in cached repo label data
/// for the repos where they are referenced (via Label Owners and Packages).
/// Report only.
/// </summary>
public class LabelNotInRepoLabelsRule(
    RepoLabelCache repoLabelCache,
    ICacheValidator cacheValidator
) : IAuditRule
{
    public int Priority => 40;
    public string RuleId => "AUD-LBL-001";
    public string Description => "Label work item doesn't exist in cached repo label data";
    public bool CanFix => false;

    public async Task<List<AuditViolation>> Evaluate(AuditContext context, CancellationToken ct)
    {
        await EnsureRepoLabelCacheIsFresh(ct);

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
                var repoLabels = GetRepoLabels(repo);
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
                    Description = $"Label '{label.LabelName}' ({label.WorkItemId}): not found in cached repo labels for [{string.Join(", ", missingRepos)}]",
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

    private HashSet<string> GetRepoLabels(string repoFullName)
    {
        var normalizedRepo = NormalizeRepoKey(repoFullName);
        if (repoLabelCache.RepoLabelDict.TryGetValue(normalizedRepo, out var cached))
        {
            return cached;
        }

        throw new InvalidOperationException(
            $"Repository label cache does not contain '{normalizedRepo}'. " +
            $"AUD-LBL-001 can only validate repos present in {DefaultStorageConstants.RepoLabelBlobStorageURI}.");
    }

    private static string NormalizeRepoKey(string repoFullName)
    {
        if (repoFullName.Contains('/'))
        {
            return repoFullName;
        }

        return $"Azure/{repoFullName}";
    }

    private async Task EnsureRepoLabelCacheIsFresh(CancellationToken ct)
    {
        DateTime minimumLastModifiedUtc = DateTime.UtcNow.Subtract(AuditRuleCacheSettings.CacheMaxAge);
        await cacheValidator.ThrowIfCacheOlderThan(DefaultStorageConstants.RepoLabelBlobStorageURI, minimumLastModifiedUtc, ct);
    }
}
