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

        return BuildViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByLabel(List<string> labels, string? repo)
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

        return BuildViewResult(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPath(string path, string? repo)
    {
        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        await HydrateLabelOwners(labelOwners);

        return BuildViewResult([], labelOwners);
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

        return BuildViewResult([packageWi], relatedLabelOwners);
    }

    // ========================
    // Add methods
    // ========================

    public async Task<string> AddOwnerToPackage(string alias, string packageName, string repo)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var ownerWi = await FindOrCreateOwner(normalizedAlias);
        var packageWi = await FindPackageByName(packageName);
        if (packageWi == null)
        {
            throw new Exception($"Package '{packageName}' not found.");
        }

        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, packageWi.WorkItemId);
        return $"Added owner '{normalizedAlias}' to package '{packageName}'.";
    }

    public async Task<string> AddOwnerToLabel(string alias, List<string> labels, string repo, string ownerType, string? path)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOrCreateOwner(normalizedAlias);

        // Validate all labels exist
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByName(labelName);
            if (labelWi == null)
            {
                throw new Exception($"Label '{labelName}' not found. Labels must be created through the central process.");
            }
            labelWorkItems.Add(labelWi);
        }

        // Find or create label owner
        var labelOwnerWi = await FindOrCreateLabelOwner(repo, labelType, path ?? "", labels);

        // Add link: Owner → Label Owner
        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, labelOwnerWi.WorkItemId);

        // Add link: Label → Label Owner for each label
        foreach (var labelWi in labelWorkItems)
        {
            await devOpsService.AddRelatedLinkAsync(labelWi.WorkItemId, labelOwnerWi.WorkItemId);
        }

        return $"Added owner '{normalizedAlias}' as {ownerType} for label(s) '{string.Join(", ", labels)}' in {repo}.";
    }

    public async Task<string> AddOwnerToPath(string alias, string repo, string path, string ownerType)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOrCreateOwner(normalizedAlias);

        var labelOwnerWi = await FindOrCreateLabelOwner(repo, labelType, path, []);

        await devOpsService.AddRelatedLinkAsync(ownerWi.WorkItemId, labelOwnerWi.WorkItemId);
        return $"Added owner '{normalizedAlias}' as {ownerType} for path '{path}' in {repo}.";
    }

    public async Task<string> AddLabelToPath(List<string> labels, string repo, string path)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByName(labelName);
            if (labelWi == null)
            {
                throw new Exception($"Label '{labelName}' not found. Labels must be created through the central process.");
            }
            labelWorkItems.Add(labelWi);
        }

        // Find existing label owner for this repo+path, or create one
        var existingLabelOwners = await QueryLabelOwnersByPath(path, repo);
        LabelOwnerWorkItem labelOwnerWi;
        if (existingLabelOwners.Count > 0)
        {
            labelOwnerWi = existingLabelOwners.First();
        }
        else
        {
            labelOwnerWi = await CreateLabelOwnerWorkItem(repo, "", path, labels);
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

    public async Task<string> RemoveOwnerFromPackage(string alias, string packageName, string repo)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{normalizedAlias}' not found.");
        }

        var packageWi = await FindPackageByName(packageName);
        if (packageWi == null)
        {
            throw new Exception($"Package '{packageName}' not found.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, packageWi.WorkItemId);
        return $"Removed owner '{normalizedAlias}' from package '{packageName}'.";
    }

    public async Task<string> RemoveOwnerFromLabel(string alias, List<string> labels, string repo, string ownerType)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{normalizedAlias}' not found.");
        }

        // Find the Label Owner matching repo+labels+type
        var allLabelOwners = await QueryAllLabelOwners(repo);
        var matchingLo = FindMatchingLabelOwner(allLabelOwners, repo, labelType, labels);
        if (matchingLo == null)
        {
            throw new Exception($"No Label Owner found for labels '{string.Join(", ", labels)}' with type '{ownerType}' in {repo}.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, matchingLo.WorkItemId);

        // Check if label owner has remaining owners and warn
        var refreshedLo = await GetWorkItemWithRelations(matchingLo.WorkItemId);
        var remainingOwnerCount = refreshedLo?.ExtractRelatedIds().Count ?? 0;
        var warning = remainingOwnerCount == 0
            ? " Warning: This Label Owner now has no remaining related work items."
            : "";

        return $"Removed owner '{normalizedAlias}' from label(s) '{string.Join(", ", labels)}' ({ownerType}) in {repo}.{warning}";
    }

    public async Task<string> RemoveOwnerFromPath(string alias, string repo, string path, string ownerType)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var labelType = ResolveOwnerType(ownerType);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias);
        if (ownerWi == null)
        {
            throw new Exception($"Owner '{normalizedAlias}' not found.");
        }

        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        var matchingLo = labelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase));
        if (matchingLo == null)
        {
            throw new Exception($"No Label Owner found for path '{path}' with type '{ownerType}' in {repo}.");
        }

        await devOpsService.RemoveRelatedLinkAsync(ownerWi.WorkItemId, matchingLo.WorkItemId);
        return $"Removed owner '{normalizedAlias}' from path '{path}' ({ownerType}) in {repo}.";
    }

    public async Task<string> RemoveLabelFromPath(List<string> labels, string repo, string path)
    {
        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        if (labelOwners.Count == 0)
        {
            throw new Exception($"No Label Owner found for path '{path}' in {repo}.");
        }

        var labelOwnerWi = labelOwners.First();

        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByName(labelName);
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

    public static string NormalizeGitHubAlias(string alias) => alias.TrimStart('@').Trim();

    public static string ResolveOwnerType(string ownerType)
    {
        if (OwnerTypeMap.TryGetValue(ownerType, out var resolved))
        {
            return resolved;
        }
        throw new Exception($"Invalid owner type '{ownerType}'. Must be one of: service-owner, azsdk-owner, pr-label.");
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

    internal async Task<OwnerWorkItem> FindOrCreateOwner(string alias)
    {
        // Validate the alias
        var validation = await codeownersValidator.ValidateCodeOwnerAsync(alias);
        if (!validation.IsValidCodeOwner)
        {
            throw new Exception($"'{alias}' is not a valid code owner. Ensure they are a member of the required GitHub organizations.");
        }

        var existing = await FindOwnerByGitHubAlias(alias);
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

    internal async Task<PackageWorkItem?> FindPackageByName(string packageName)
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

    internal async Task<LabelWorkItem?> FindLabelByName(string labelName)
    {
        var workItems = await devOpsService.QueryWorkItemsByTypeAndFieldAsync("Label", "Custom.Label", labelName);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToLabelWorkItem(workItems.First());
    }

    internal async Task<LabelOwnerWorkItem> FindOrCreateLabelOwner(string repo, string labelType, string repoPath, List<string> labels)
    {
        // Try to find an existing one matching repo+path+labelType+labels
        var allLabelOwners = !string.IsNullOrEmpty(repoPath)
            ? await QueryLabelOwnersByPath(repoPath, repo)
            : await QueryAllLabelOwners(repo);

        // Hydrate label owners to populate their .Labels collections from RelatedIds
        await HydrateLabelOwners(allLabelOwners);

        // Resolve expected label names to work item IDs
        var expectedLabelIds = new HashSet<int>();
        foreach (var labelName in labels)
        {
            var labelWi = await FindLabelByName(labelName);
            if (labelWi != null)
            {
                expectedLabelIds.Add(labelWi.WorkItemId);
            }
        }

        var match = allLabelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase) &&
            lo.Repository.Equals(repo, StringComparison.OrdinalIgnoreCase) &&
            lo.RepoPath.Equals(repoPath, StringComparison.OrdinalIgnoreCase) &&
            lo.Labels.Select(l => l.WorkItemId).ToHashSet().SetEquals(expectedLabelIds)
        );

        if (match != null)
        {
            return match;
        }

        return await CreateLabelOwnerWorkItem(repo, labelType, repoPath, labels);
    }

    private async Task<LabelOwnerWorkItem> CreateLabelOwnerWorkItem(string repo, string labelType, string repoPath, List<string> labels)
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
            var repoName = repo.Contains('/') ? repo.Split('/').Last() : repo;
            var language = SdkLanguageHelpers.GetLanguageForRepo(repoName);
            if (language != SdkLanguage.Unknown)
            {
                var languageString = language.ToWorkItemString();
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

    private async Task<List<LabelOwnerWorkItem>> QueryAllLabelOwners(string? repo)
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

    private async Task<WorkItem?> GetWorkItemWithRelations(int workItemId)
    {
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(
            $"SELECT [System.Id] FROM WorkItems WHERE [System.Id] = {workItemId}",
            expand: WorkItemExpand.Relations);
        return workItems.FirstOrDefault();
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
            package.Owners.Clear();
            package.Labels.Clear();
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
                WorkItemId = p.WorkItemId,
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

        // Path-based: group by repo+path, sorted by repo then path
        if (pathBased.Count > 0)
        {
            result.PathBasedLabelOwners = pathBased
                .GroupBy(lo => (lo.Repository, lo.RepoPath), new RepoPathComparer())
                .Select(g => new LabelOwnerGroup
                {
                    Path = g.Key.RepoPath,
                    Repo = g.Key.Repository,
                    Items = g.Select(lo => new LabelOwnerViewItem
                    {
                        WorkItemId = lo.WorkItemId,
                        Repo = lo.Repository,
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                        Labels = lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList()
                    }).ToList()
                })
                .OrderBy(g => g.Repo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Pathless: group by repo, sorted by repo then label set
        if (pathless.Count > 0)
        {
            result.PathlessLabelOwners = pathless
                .OrderBy(lo => lo.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lo => string.Join("|", lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
                .Select(lo => new LabelOwnerGroup
                {
                    Repo = lo.Repository,
                    LabelSet = lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList(),
                    Items = [new LabelOwnerViewItem
                    {
                        WorkItemId = lo.WorkItemId,
                        Repo = lo.Repository,
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList()
                    }]
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

    private class RepoPathComparer : IEqualityComparer<(string Repository, string RepoPath)>
    {
        public bool Equals((string Repository, string RepoPath) x, (string Repository, string RepoPath) y) =>
            string.Equals(x.Repository, y.Repository, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.RepoPath, y.RepoPath, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Repository, string RepoPath) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Repository),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RepoPath));
    }
}
