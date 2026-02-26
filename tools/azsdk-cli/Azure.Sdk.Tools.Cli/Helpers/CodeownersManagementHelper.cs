// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Business logic layer for CODEOWNERS management operations on Azure DevOps work items.
/// </summary>
public class CodeownersManagementHelper(
    IDevOpsService devOpsService
) : ICodeownersManagementHelper
{

    // ========================
    // View methods
    // ========================

    public async Task<CodeownersViewResult> GetViewByUser(string alias, string? repo)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias);
        if (ownerWi == null)
        {
            return new CodeownersViewResult { ResponseError = $"No Owner work item found for alias '{normalizedAlias}'." };
        }

        var relatedPackages = await FetchRelatedPackages(ownerWi.RelatedIds, repo);
        var relatedLabelOwners = await FetchRelatedLabelOwners(ownerWi.RelatedIds, repo);

        await HydratePackages(relatedPackages);
        await HydrateLabelOwners(relatedLabelOwners);

        return new CodeownersViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByLabel(string[] labels, string? repo)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var label in labels)
        {
            var labelWi = await FindLabelByName(label);
            if (labelWi == null)
            {
                return new CodeownersViewResult { ResponseError = $"No Label work item found for '{label}'." };
            }
            labelWorkItems.Add(labelWi);
        }

        // Intersect related IDs from all labels (AND semantics)
        var commonRelatedIds = labelWorkItems
            .Select(l => l.RelatedIds.AsEnumerable())
            .Aggregate((a, b) => a.Intersect(b))
            .ToList();

        var relatedPackages = await FetchRelatedPackages(commonRelatedIds, repo);
        var relatedLabelOwners = await FetchRelatedLabelOwners(commonRelatedIds, repo);

        await HydratePackages(relatedPackages);
        await HydrateLabelOwners(relatedLabelOwners);

        return new CodeownersViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPath(string path, string? repo)
    {
        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        await HydrateLabelOwners(labelOwners);

        return new CodeownersViewResult([], labelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPackage(string packageName)
    {
        var packageWi = await FindPackageByName(packageName);
        if (packageWi == null)
        {
            return new CodeownersViewResult { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        await HydratePackages([packageWi]);

        var relatedLabelOwners = await FetchRelatedLabelOwners(packageWi.RelatedIds);
        await HydrateLabelOwners(relatedLabelOwners);

        return new CodeownersViewResult([packageWi], relatedLabelOwners);
    }

    // ========================
    // Internal helpers
    // ========================

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

    private async Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Owner", "Custom.GitHubAlias", alias);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToOwnerWorkItem(workItems.First());
    }

    private async Task<PackageWorkItem?> FindPackageByName(string packageName)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Package", "Custom.Package", packageName);
        if (workItems.Count == 0)
        {
            return null;
        }

        var packages = workItems.Select(WorkItemMappers.MapToPackageWorkItem).ToList();
        var latest = WorkItemMappers.GetLatestPackageVersions(packages);
        return latest.FirstOrDefault();
    }

    private async Task<LabelWorkItem?> FindLabelByName(string labelName)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Label", "Custom.Label", labelName);
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
    /// Fetches Package work items from a set of work item IDs, filtering by repo language and deduplicating versions.
    /// </summary>
    private async Task<List<PackageWorkItem>> FetchRelatedPackages(IEnumerable<int> relatedIds, string? repo = null)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds, expand: WorkItemExpand.Relations);

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

        return WorkItemMappers.GetLatestPackageVersions(packages);
    }

    /// <summary>
    /// Fetches Label Owner work items from a set of work item IDs, optionally filtering by repo.
    /// </summary>
    private async Task<List<LabelOwnerWorkItem>> FetchRelatedLabelOwners(IEnumerable<int> relatedIds, string? repo = null)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds, expand: WorkItemExpand.Relations);

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

    private async Task<List<LabelOwnerWorkItem>> QueryLabelOwnersByPath(string path, string? repo)
    {
        var escapedPath = path.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label Owner' AND [Custom.RepoPath] = '{escapedPath}'";
        if (!string.IsNullOrEmpty(repo))
        {
            var escapedRepo = repo.Replace("'", "''");
            query += $" AND [Custom.Repository] = '{escapedRepo}'";
        }
        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        return rawWorkItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
    }

    // ========================
    // Hydration helpers
    // ========================

    /// <summary>
    /// Fetches Owner and Label work items for a set of related IDs, returning them as dictionaries keyed by work item ID.
    /// </summary>
    private async Task<(Dictionary<int, OwnerWorkItem> Owners, Dictionary<int, LabelWorkItem> Labels)> FetchRelatedOwnersAndLabels(List<int> relatedIds)
    {
        var workItems = await devOpsService.GetWorkItemsByIdsAsync(relatedIds);

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

    private async Task HydrateLabelOwners(List<LabelOwnerWorkItem> labelOwners)
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

        var (owners, labels) = await FetchRelatedOwnersAndLabels(allRelatedIds);

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
        }
    }

    private async Task HydratePackages(List<PackageWorkItem> packages)
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

        var (owners, labels) = await FetchRelatedOwnersAndLabels(allRelatedIds);

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
        }
    }

}
