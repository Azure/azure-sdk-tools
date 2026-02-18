// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Business logic layer for CODEOWNERS management operations on Azure DevOps work items.
/// </summary>
public class CodeownersManagementHelper(
    IDevOpsService devOpsService,
    ICodeownersValidatorHelper codeownersValidator,
    ILogger<CodeownersManagementHelper> logger
) : ICodeownersManagementHelper
{
    private static readonly Dictionary<string, string> OwnerTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "service-owner", "Service Owner" },
        { "azsdk-owner", "Azure SDK Owner" },
        { "pr-label", "PR Label" }
    };

    // ========================
    // View methods
    // ========================

    public async Task<CodeownersViewResult> GetViewByUserAsync(string alias, string? repo)
    {
        alias = NormalizeAlias(alias);
        var ownerWi = await FindOwnerByAliasAsync(alias);
        if (ownerWi == null)
        {
            return new CodeownersViewResult { ResponseError = $"No Owner work item found for alias '{alias}'." };
        }

        // Find all packages related to this owner
        var allPackages = await QueryPackagesAsync();
        var relatedPackages = allPackages
            .Where(p => p.RelatedIds.Contains(ownerWi.WorkItemId))
            .ToList();

        // Find all label owners related to this owner
        var allLabelOwners = await QueryAllLabelOwnersAsync(repo);
        var relatedLabelOwners = allLabelOwners
            .Where(lo => lo.RelatedIds.Contains(ownerWi.WorkItemId))
            .ToList();

        // Hydrate label owners
        await HydrateLabelOwnersAsync(relatedLabelOwners);

        return BuildViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByLabelAsync(string label, string? repo)
    {
        var labelWi = await FindLabelByNameAsync(label);
        if (labelWi == null)
        {
            return new CodeownersViewResult { ResponseError = $"No Label work item found for '{label}'." };
        }

        // Find packages related to this label
        var allPackages = await QueryPackagesAsync();
        var relatedPackages = allPackages
            .Where(p => p.RelatedIds.Contains(labelWi.WorkItemId))
            .ToList();

        // Find label owners related to this label
        var allLabelOwners = await QueryAllLabelOwnersAsync(repo);
        var relatedLabelOwners = allLabelOwners
            .Where(lo => lo.RelatedIds.Contains(labelWi.WorkItemId))
            .ToList();

        await HydrateLabelOwnersAsync(relatedLabelOwners);

        return BuildViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPathAsync(string path, string? repo)
    {
        var labelOwners = await QueryLabelOwnersByPathAsync(path, repo);
        await HydrateLabelOwnersAsync(labelOwners);

        return BuildViewResult([], labelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPackageAsync(string packageName)
    {
        var packageWi = await FindPackageByNameAsync(packageName);
        if (packageWi == null)
        {
            return new CodeownersViewResult { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        // Hydrate the package
        await HydratePackageAsync(packageWi);

        // Find related label owners
        var allLabelOwners = await QueryAllLabelOwnersAsync(null);
        var relatedLabelOwners = allLabelOwners
            .Where(lo => packageWi.RelatedIds.Contains(lo.WorkItemId))
            .ToList();
        await HydrateLabelOwnersAsync(relatedLabelOwners);

        return BuildViewResult([packageWi], relatedLabelOwners);
    }

    // ========================
    // Add methods
    // ========================

    public async Task<string> AddOwnerToPackageAsync(string alias, string packageName, string repo)
    {
        alias = NormalizeAlias(alias);
        var ownerWi = await FindOrCreateOwnerAsync(alias);
        var packageWi = await FindPackageByNameAsync(packageName);
        if (packageWi == null)
        {
            throw new Exception($"Package '{packageName}' not found.");
        }

        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, packageWi.WorkItemId);
        return $"Added owner '{alias}' to package '{packageName}'.";
    }

    public async Task<string> AddOwnerToLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path)
    {
        alias = NormalizeAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOrCreateOwnerAsync(alias);

        // Validate all labels exist
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                throw new Exception($"Label '{labelName}' not found. Labels must be created through the central process.");
            }
            labelWorkItems.Add(labelWi);
        }

        // Find or create label owner
        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, labelType, path ?? "", labels);

        // Add link: Owner → Label Owner
        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, labelOwnerWi.WorkItemId);

        // Add link: Label → Label Owner for each label
        foreach (var labelWi in labelWorkItems)
        {
            await devOpsService.AddRelatedLinkAsync(labelWi.WorkItemId, labelOwnerWi.WorkItemId);
        }

        return $"Added owner '{alias}' as {ownerType} for label(s) '{string.Join(", ", labels)}' in {repo}.";
    }

    public async Task<string> AddOwnerToPathAsync(string alias, string repo, string path, string ownerType)
    {
        alias = NormalizeAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOrCreateOwnerAsync(alias);

        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, labelType, path, []);

        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, labelOwnerWi.WorkItemId);
        return $"Added owner '{alias}' as {ownerType} for path '{path}' in {repo}.";
    }

    public async Task<string> AddLabelToPathAsync(List<string> labels, string repo, string path)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                throw new Exception($"Label '{labelName}' not found. Labels must be created through the central process.");
            }
            labelWorkItems.Add(labelWi);
        }

        // Find existing label owner for this repo+path, or create one
        var existingLabelOwners = await QueryLabelOwnersByPathAsync(path, repo);
        LabelOwnerWorkItem labelOwnerWi;
        if (existingLabelOwners.Count > 0)
        {
            labelOwnerWi = existingLabelOwners.First();
        }
        else
        {
            labelOwnerWi = await CreateLabelOwnerWorkItemAsync(repo, "", path, labels);
        }

        foreach (var labelWi in labelWorkItems)
        {
            await devOpsService.AddRelatedLinkAsync(labelWi.WorkItemId, labelOwnerWi.WorkItemId);
        }

        return $"Added label(s) '{string.Join(", ", labels)}' to path '{path}' in {repo}.";
    }

    // ========================
    // Remove methods
    // ========================

    public async Task<string> RemoveOwnerFromPackageAsync(string alias, string packageName, string repo)
    {
        alias = NormalizeAlias(alias);
        var ownerWi = await FindOwnerByAliasAsync(alias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{alias}' not found.");
        }

        var packageWi = await FindPackageByNameAsync(packageName);
        if (packageWi == null)
        {
            throw new Exception($"Package '{packageName}' not found.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, packageWi.WorkItemId);
        return $"Removed owner '{alias}' from package '{packageName}'.";
    }

    public async Task<string> RemoveOwnerFromLabelAsync(string alias, List<string> labels, string repo, string ownerType)
    {
        alias = NormalizeAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOwnerByAliasAsync(alias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{alias}' not found.");
        }

        // Find the Label Owner matching repo+labels+type
        var allLabelOwners = await QueryAllLabelOwnersAsync(repo);
        var matchingLo = FindMatchingLabelOwner(allLabelOwners, repo, labelType, labels);
        if (matchingLo == null)
        {
            throw new Exception($"No Label Owner found for labels '{string.Join(", ", labels)}' with type '{ownerType}' in {repo}.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, matchingLo.WorkItemId);

        // Check if label owner has remaining owners and warn
        var refreshedLo = await GetWorkItemWithRelationsAsync(matchingLo.WorkItemId);
        var remainingOwnerCount = refreshedLo?.ExtractRelatedIds().Count ?? 0;
        var warning = remainingOwnerCount == 0
            ? " Warning: This Label Owner now has no remaining related work items."
            : "";

        return $"Removed owner '{alias}' from label(s) '{string.Join(", ", labels)}' ({ownerType}) in {repo}.{warning}";
    }

    public async Task<string> RemoveOwnerFromPathAsync(string alias, string repo, string path, string ownerType)
    {
        alias = NormalizeAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOwnerByAliasAsync(alias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{alias}' not found.");
        }

        var labelOwners = await QueryLabelOwnersByPathAsync(path, repo);
        var matchingLo = labelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase));
        if (matchingLo == null)
        {
            throw new Exception($"No Label Owner found for path '{path}' with type '{ownerType}' in {repo}.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, matchingLo.WorkItemId);
        return $"Removed owner '{alias}' from path '{path}' ({ownerType}) in {repo}.";
    }

    public async Task<string> RemoveLabelFromPathAsync(List<string> labels, string repo, string path)
    {
        var labelOwners = await QueryLabelOwnersByPathAsync(path, repo);
        if (labelOwners.Count == 0)
        {
            throw new Exception($"No Label Owner found for path '{path}' in {repo}.");
        }

        var labelOwnerWi = labelOwners.First();

        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                throw new Exception($"Label '{labelName}' not found.");
            }
            await devOpsService.RemoveRelatedLinkAsync(labelWi.WorkItemId, labelOwnerWi.WorkItemId);
        }

        return $"Removed label(s) '{string.Join(", ", labels)}' from path '{path}' in {repo}.";
    }

    // ========================
    // Internal helpers
    // ========================

    internal static string NormalizeAlias(string alias) => alias.TrimStart('@').Trim();

    internal static string ResolveOwnerType(string ownerType)
    {
        if (OwnerTypeMap.TryGetValue(ownerType, out var resolved))
        {
            return resolved;
        }
        throw new Exception($"Invalid owner type '{ownerType}'. Must be one of: service-owner, azsdk-owner, pr-label.");
    }

    private async Task<OwnerWorkItem?> FindOwnerByAliasAsync(string alias)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Owner", "Custom.GitHubAlias", alias);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToOwnerWorkItem(workItems.First());
    }

    internal async Task<OwnerWorkItem> FindOrCreateOwnerAsync(string alias)
    {
        // Validate the alias
        var validation = await codeownersValidator.ValidateCodeOwnerAsync(alias);
        if (!validation.IsValidCodeOwner)
        {
            throw new Exception($"'{alias}' is not a valid code owner. Ensure they are a member of the required GitHub organizations.");
        }

        var existing = await FindOwnerByAliasAsync(alias);
        if (existing != null)
        {
            return existing;
        }

        logger.LogInformation("Creating new Owner work item for alias '{Alias}'", alias);
        var fields = new Dictionary<string, object>
        {
            { "System.Title", $"Owner {alias}" },
            { "Custom.GitHubAlias", alias }
        };
        var created = await devOpsService.CreateTypedWorkItemAsync("Owner", fields);
        return WorkItemMappers.MapToOwnerWorkItem(created);
    }

    internal async Task<PackageWorkItem?> FindPackageByNameAsync(string packageName)
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

    internal async Task<LabelWorkItem?> FindLabelByNameAsync(string labelName)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Label", "Custom.Label", labelName);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToLabelWorkItem(workItems.First());
    }

    internal async Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(string repo, string labelType, string repoPath, List<string> labels)
    {
        // Try to find an existing one matching repo+path+labelType
        var allLabelOwners = !string.IsNullOrEmpty(repoPath)
            ? await QueryLabelOwnersByPathAsync(repoPath, repo)
            : await QueryAllLabelOwnersAsync(repo);

        var match = allLabelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase) &&
            lo.Repository.Equals(repo, StringComparison.OrdinalIgnoreCase) &&
            lo.RepoPath.Equals(repoPath, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return match;
        }

        return await CreateLabelOwnerWorkItemAsync(repo, labelType, repoPath, labels);
    }

    private async Task<LabelOwnerWorkItem> CreateLabelOwnerWorkItemAsync(string repo, string labelType, string repoPath, List<string> labels)
    {
        var labelList = labels.Count > 0 ? string.Join(", ", labels.OrderBy(l => l, StringComparer.OrdinalIgnoreCase)) : "(no labels)";
        var title = $"Label Owner: {repo} - {labelType} - {labelList}";

        var fields = new Dictionary<string, object>
        {
            { "System.Title", title },
            { "Custom.LabelType", labelType },
            { "Custom.Repository", repo },
            { "Custom.RepoPath", repoPath }
        };

        logger.LogInformation("Creating new Label Owner work item: {Title}", title);
        var created = await devOpsService.CreateTypedWorkItemAsync("Label Owner", fields);
        return WorkItemMappers.MapToLabelOwnerWorkItem(created);
    }

    // ========================
    // Query helpers
    // ========================

    private async Task<List<PackageWorkItem>> QueryPackagesAsync()
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Package", "System.WorkItemType", "Package");
        // The above is a workaround; let's use FetchWorkItemsPagedAsync directly
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Package'";
        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        var packages = rawWorkItems.Select(WorkItemMappers.MapToPackageWorkItem).ToList();
        return WorkItemMappers.GetLatestPackageVersions(packages);
    }

    private async Task<List<LabelOwnerWorkItem>> QueryAllLabelOwnersAsync(string? repo)
    {
        string query;
        if (!string.IsNullOrEmpty(repo))
        {
            var escapedRepo = repo.Replace("'", "''");
            query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label Owner' AND [Custom.Repository] = '{escapedRepo}'";
        }
        else
        {
            query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label Owner'";
        }
        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        return rawWorkItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
    }

    private async Task<List<LabelOwnerWorkItem>> QueryLabelOwnersByPathAsync(string path, string? repo)
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

    private async Task<WorkItem?> GetWorkItemWithRelationsAsync(int workItemId)
    {
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(
            $"SELECT [System.Id] FROM WorkItems WHERE [System.Id] = {workItemId}",
            expand: WorkItemExpand.Relations);
        return workItems.FirstOrDefault();
    }

    // ========================
    // Hydration helpers
    // ========================

    private async Task HydrateLabelOwnersAsync(List<LabelOwnerWorkItem> labelOwners)
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

        // Fetch all potentially related work items
        var ownerQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Owner'";
        var labelQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label'";

        var ownerWorkItems = await devOpsService.FetchWorkItemsPagedAsync(ownerQuery);
        var labelWorkItems = await devOpsService.FetchWorkItemsPagedAsync(labelQuery);

        var owners = ownerWorkItems
            .Where(wi => wi.Id.HasValue && allRelatedIds.Contains(wi.Id.Value))
            .Select(WorkItemMappers.MapToOwnerWorkItem)
            .ToDictionary(o => o.WorkItemId);

        var labels = labelWorkItems
            .Where(wi => wi.Id.HasValue && allRelatedIds.Contains(wi.Id.Value))
            .Select(WorkItemMappers.MapToLabelWorkItem)
            .ToDictionary(l => l.WorkItemId);

        foreach (var lo in labelOwners)
        {
            lo.Owners.Clear();
            lo.Labels.Clear();
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

    private async Task HydratePackageAsync(PackageWorkItem packageWi)
    {
        var allRelatedIds = packageWi.RelatedIds.ToList();
        if (allRelatedIds.Count == 0)
        {
            return;
        }

        var ownerQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Owner'";
        var labelQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label'";

        var ownerWorkItems = await devOpsService.FetchWorkItemsPagedAsync(ownerQuery);
        var labelWorkItems = await devOpsService.FetchWorkItemsPagedAsync(labelQuery);

        packageWi.Owners.Clear();
        packageWi.Labels.Clear();

        foreach (var id in allRelatedIds)
        {
            var ownerWi = ownerWorkItems.FirstOrDefault(wi => wi.Id == id);
            if (ownerWi != null)
            {
                packageWi.Owners.Add(WorkItemMappers.MapToOwnerWorkItem(ownerWi));
                continue;
            }
            var labelWi = labelWorkItems.FirstOrDefault(wi => wi.Id == id);
            if (labelWi != null)
            {
                packageWi.Labels.Add(WorkItemMappers.MapToLabelWorkItem(labelWi));
            }
        }
    }

    // ========================
    // View result building
    // ========================

    private static CodeownersViewResult BuildViewResult(List<PackageWorkItem> packages, List<LabelOwnerWorkItem> labelOwners)
    {
        var result = new CodeownersViewResult();

        // Build package view items
        if (packages.Count > 0)
        {
            result.Packages = packages.Select(p => new PackageViewItem
            {
                PackageName = p.PackageName,
                Language = p.Language,
                PackageType = p.PackageType,
                SourceOwners = p.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                Labels = p.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList()
            }).ToList();
        }

        // Split label owners into path-based and pathless
        var pathBased = labelOwners.Where(lo => !string.IsNullOrEmpty(lo.RepoPath)).ToList();
        var pathless = labelOwners.Where(lo => string.IsNullOrEmpty(lo.RepoPath)).ToList();

        // Path-based: group by path
        if (pathBased.Count > 0)
        {
            result.PathBasedLabelOwners = pathBased
                .GroupBy(lo => lo.RepoPath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LabelOwnerGroup
                {
                    Path = g.Key,
                    Repo = g.First().Repository,
                    Items = g.Select(lo => new LabelOwnerViewItem
                    {
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                        Labels = lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList()
                    }).ToList()
                }).ToList();
        }

        // Pathless: group by alphabetized label set
        if (pathless.Count > 0)
        {
            result.PathlessLabelOwners = pathless
                .GroupBy(lo => string.Join("|", lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LabelOwnerGroup
                {
                    LabelSet = g.First().Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList(),
                    Items = g.Select(lo => new LabelOwnerViewItem
                    {
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList()
                    }).ToList()
                }).ToList();
        }

        return result;
    }

    private static LabelOwnerWorkItem? FindMatchingLabelOwner(List<LabelOwnerWorkItem> labelOwners, string repo, string labelType, List<string> labels)
    {
        // For remove operations: find label owner by repo+type, then check labels match
        return labelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase) &&
            lo.Repository.Equals(repo, StringComparison.OrdinalIgnoreCase));
    }
}
