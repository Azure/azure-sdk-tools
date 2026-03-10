// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class CodeownersManagementHelper(
    IDevOpsService devOpsService,
    ITeamUserCache teamUserCache
) : ICodeownersManagementHelper
{
    public async Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo, CancellationToken ct)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias, ct);
        if (ownerWi == null)
        {
            return new CodeownersViewResponse { ResponseError = $"No Owner work item found for alias '{normalizedAlias}'." };
        }

        var (relatedPackages, relatedLabelOwners) = await FetchRelatedPackagesAndLabelOwners(ownerWi.RelatedIds, repo, ct);

        await HydratePackages(relatedPackages, ct);
        await HydrateLabelOwners(relatedLabelOwners, ct);

        return new CodeownersViewResponse(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo, CancellationToken ct)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var label in labels)
        {
            var labelWi = await FindLabelByName(label, ct);
            if (labelWi == null)
            {
                return new CodeownersViewResponse { ResponseError = $"No Label work item found for '{label}'." };
            }
            labelWorkItems.Add(labelWi);
        }

        // Intersect related IDs from all labels (AND semantics)
        var commonRelatedIds = labelWorkItems
            .Select(l => l.RelatedIds.AsEnumerable())
            .Aggregate((a, b) => a.Intersect(b))
            .ToList();

        var (relatedPackages, relatedLabelOwners) = await FetchRelatedPackagesAndLabelOwners(commonRelatedIds, repo, ct);

        await HydratePackages(relatedPackages, ct);
        await HydrateLabelOwners(relatedLabelOwners, ct);

        return new CodeownersViewResponse(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByPath(string path, string? repo, CancellationToken ct)
    {
        var labelOwners = await QueryLabelOwnersByPath(path, repo, ct);
        await HydrateLabelOwners(labelOwners, ct);

        return new CodeownersViewResponse([], labelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null, CancellationToken ct = default)
    {
        var packageWi = await FindPackageByName(packageName, repo, ct);
        if (packageWi == null)
        {
            return new CodeownersViewResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        await HydratePackages([packageWi], ct);

        var relatedLabelOwners = await FetchRelatedLabelOwners(packageWi.RelatedIds, repo, ct);
        await HydrateLabelOwners(relatedLabelOwners, ct);

        return new CodeownersViewResponse([packageWi], relatedLabelOwners);
    }

    public static string NormalizeGitHubAlias(string alias) => alias.Trim().TrimStart('@').Trim();

    /// <summary>
    /// Converts a repo identity (e.g., "Azure/azure-sdk-for-python" or "azure-sdk-for-python")
    /// to its SdkLanguage work item string (e.g., "Python"). Falls back to the original value
    /// if the repo is not a known language repo.
    /// </summary>
    internal static string RepoToLanguageString(string repo)
    {
        var repoName = repo.Contains('/') ? repo.Split('/').Last() : repo;
        var language = SdkLanguageHelpers.GetLanguageForRepo(repoName);
        return language != SdkLanguage.Unknown ? language.ToWorkItemString() : repo;
    }

    private async Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias, CancellationToken ct)
    {
        var escapedAlias = alias.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Owner' AND [Custom.GitHubAlias] = '{escapedAlias}'";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToOwnerWorkItem(workItems.First());
    }

    private async Task<PackageWorkItem?> FindPackageByName(string packageName, string? repo = null, CancellationToken ct = default)
    {
        var escapedPackageName = packageName.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Package' AND [Custom.Package] = '{escapedPackageName}'";

        if (!string.IsNullOrEmpty(repo))
        {
            var languageString = RepoToLanguageString(repo);
            if (languageString != repo)
            {
                var escapedLanguage = languageString.Replace("'", "''");
                query += $" AND [Custom.Language] = '{escapedLanguage}'";
            }
        }

        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        if (workItems.Count == 0)
        {
            return null;
        }

        var packages = workItems.Select(WorkItemMappers.MapToPackageWorkItem).ToList();
        var latest = WorkItemMappers.GetLatestPackageVersions(packages);
        return latest.FirstOrDefault();
    }

    private async Task<LabelWorkItem?> FindLabelByName(string labelName, CancellationToken ct)
    {
        var escapedLabel = labelName.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label' AND [Custom.Label] = '{escapedLabel}'";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToLabelWorkItem(workItems.First());
    }

    // ========================
    // Query helpers
    // ========================

    /// <summary>
    /// Fetches Package and Label Owner work items from a set of work item IDs in a single API call,
    /// filtering packages by repo language and deduplicating versions, and optionally filtering label owners by repo.
    /// </summary>
    private async Task<(List<PackageWorkItem> Packages, List<LabelOwnerWorkItem> LabelOwners)> FetchRelatedPackagesAndLabelOwners(IEnumerable<int> relatedIds, string? repo = null, CancellationToken ct = default)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds, expand: WorkItemExpand.Relations, ct: ct);

        var packages = workItems
            .Where(wi => wi.Fields.TryGetValue("System.WorkItemType", out var type) && type?.ToString() == "Package")
            .Select(WorkItemMappers.MapToPackageWorkItem)
            .ToList();

        if (!string.IsNullOrEmpty(repo))
        {
            var languageString = RepoToLanguageString(repo);
            if (languageString != repo)
            {
                packages = packages.Where(p => p.Language.Equals(languageString, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        packages = WorkItemMappers.GetLatestPackageVersions(packages);

        var labelOwners = workItems
            .Where(wi => wi.Fields.TryGetValue("System.WorkItemType", out var type) && type?.ToString() == "Label Owner")
            .Select(WorkItemMappers.MapToLabelOwnerWorkItem)
            .ToList();

        if (!string.IsNullOrEmpty(repo))
        {
            labelOwners = labelOwners.Where(lo => lo.Repository.Equals(repo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return (packages, labelOwners);
    }

    /// <summary>
    /// Fetches Label Owner work items from a set of work item IDs, optionally filtering by repo.
    /// </summary>
    private async Task<List<LabelOwnerWorkItem>> FetchRelatedLabelOwners(IEnumerable<int> relatedIds, string? repo = null, CancellationToken ct = default)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds, expand: WorkItemExpand.Relations, ct: ct);

        var labelOwners = workItems
            .Where(wi => wi.Fields.TryGetValue("System.WorkItemType", out var type) && type?.ToString() == "Label Owner")
            .Select(WorkItemMappers.MapToLabelOwnerWorkItem)
            .ToList();

        if (!string.IsNullOrEmpty(repo))
        {
            labelOwners = labelOwners.Where(lo => lo.Repository.Equals(repo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return labelOwners;
    }

    private async Task<List<LabelOwnerWorkItem>> QueryLabelOwnersByPath(string path, string? repo, CancellationToken ct)
    {
        var escapedPath = path.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [System.WorkItemType] = 'Label Owner' AND [Custom.RepoPath] = '{escapedPath}'";
        if (!string.IsNullOrEmpty(repo))
        {
            var escapedRepo = repo.Replace("'", "''");
            query += $" AND [Custom.Repository] = '{escapedRepo}'";
        }
        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        return rawWorkItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
    }

    // ========================
    // Hydration helpers
    // ========================

    /// <summary>
    /// Fetches Owner and Label work items for a set of related IDs, returning them as dictionaries keyed by work item ID.
    /// </summary>
    private async Task<(Dictionary<int, OwnerWorkItem> Owners, Dictionary<int, LabelWorkItem> Labels)> FetchRelatedOwnersAndLabels(List<int> relatedIds, CancellationToken ct)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds, ct: ct);

        var owners = workItems
            .Where(wi => wi.Fields.TryGetValue("System.WorkItemType", out var type) && type?.ToString() == "Owner")
            .Select(WorkItemMappers.MapToOwnerWorkItem)
            .ToDictionary(o => o.WorkItemId);

        var labels = workItems
            .Where(wi => wi.Fields.TryGetValue("System.WorkItemType", out var type) && type?.ToString() == "Label")
            .Select(WorkItemMappers.MapToLabelWorkItem)
            .ToDictionary(l => l.WorkItemId);

        return (owners, labels);
    }

    private async Task HydrateLabelOwners(List<LabelOwnerWorkItem> labelOwners, CancellationToken ct)
    {
        if (labelOwners.Count == 0)
        {
            return;
        }

        // Collect all related IDs from label owners
        var allRelatedIds = labelOwners.SelectMany(lo => lo.RelatedIds).Distinct().ToList();
        if (allRelatedIds.Count == 0)
        {
            return;
        }

        var (owners, labels) = await FetchRelatedOwnersAndLabels(allRelatedIds, ct);

        foreach (var lo in labelOwners)
        {
            foreach (var id in lo.RelatedIds)
            {
                if (owners.TryGetValue(id, out var owner))
                {
                    lo.Owners.Add(owner);
                }
                else if (labels.TryGetValue(id, out var label))
                {
                    lo.Labels.Add(label);
                }
            }
            ExpandTeamOwners(lo.Owners);
        }
    }

    private async Task HydratePackages(List<PackageWorkItem> packages, CancellationToken ct)
    {
        if (packages.Count == 0)
        {
            return;
        }

        var allRelatedIds = packages.SelectMany(p => p.RelatedIds).Distinct().ToList();
        if (allRelatedIds.Count == 0)
        {
            return;
        }

        var (owners, labels) = await FetchRelatedOwnersAndLabels(allRelatedIds, ct);

        foreach (var package in packages)
        {
            foreach (var id in package.RelatedIds)
            {
                if (owners.TryGetValue(id, out var owner))
                {
                    package.Owners.Add(owner);
                }
                else if (labels.TryGetValue(id, out var label))
                {
                    package.Labels.Add(label);
                }
            }
            ExpandTeamOwners(package.Owners);
        }
    }

    /// <summary>
    /// For each owner that is a GitHub team, expands it to individual members
    /// using the CodeownersUtils TeamUserCache.
    /// </summary>
    private void ExpandTeamOwners(List<OwnerWorkItem> owners)
    {
        foreach (var owner in owners)
        {
            if (owner.IsGitHubTeam)
            {
                var members = teamUserCache.GetUsersForTeam(owner.GitHubAlias);
                if (members.Count > 0)
                {
                    owner.ExpandedMembers = members;
                }
            }
        }
    }

}
