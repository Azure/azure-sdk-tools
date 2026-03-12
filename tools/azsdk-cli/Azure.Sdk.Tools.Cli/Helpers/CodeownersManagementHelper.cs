// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Helpers;

public enum OwnerType
{
    ServiceOwner,
    AzSdkOwner,
    PrLabel
}

public static class OwnerTypeExtensions
{
    public static string ToWorkItemString(this OwnerType ownerType) => ownerType switch
    {
        OwnerType.ServiceOwner => "Service Owner",
        OwnerType.AzSdkOwner   => "Azure SDK Owner",
        OwnerType.PrLabel      => "PR Label",
        _ => throw new ArgumentOutOfRangeException(nameof(ownerType), ownerType, $"Unknown owner type '{ownerType}'.")
    };
}


public class CodeownersManagementHelper(
    ILogger<CodeownersManagementHelper> logger,
    IDevOpsService devOpsService,
    ITeamUserCache teamUserCache
) : ICodeownersManagementHelper
{
    public async Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var ownerWi = await FindOwnerByGitHubAlias(normalizedAlias);
        if (ownerWi == null)
        {
            return new CodeownersViewResponse { ResponseError = $"No Owner work item found for alias '{normalizedAlias}'." };
        }

        var (relatedPackages, relatedLabelOwners) = await FetchRelatedPackagesAndLabelOwners(ownerWi.RelatedIds, repo);

        await HydratePackages(relatedPackages);
        await HydrateLabelOwners(relatedLabelOwners);

        return new CodeownersViewResponse(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var label in labels)
        {
            var labelWi = await FindLabelByName(label);
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

        var (relatedPackages, relatedLabelOwners) = await FetchRelatedPackagesAndLabelOwners(commonRelatedIds, repo);

        await HydratePackages(relatedPackages);
        await HydrateLabelOwners(relatedLabelOwners);

        return new CodeownersViewResponse(relatedPackages, relatedLabelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByPath(string path, string? repo)
    {
        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        await HydrateLabelOwners(labelOwners);

        return new CodeownersViewResponse([], labelOwners);
    }

    public async Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersViewResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        await HydratePackages([packageWi]);

        var relatedLabelOwners = await FetchRelatedLabelOwners(packageWi.RelatedIds, repo);
        await HydrateLabelOwners(relatedLabelOwners);

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

    public async Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var escapedAlias = normalizedAlias.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Owner' AND [Custom.GitHubAlias] = '{escapedAlias}'";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        if (workItems.Count == 0)
        {
            return null;
        }
        return WorkItemMappers.MapToOwnerWorkItem(workItems.First());
    }

    private async Task<PackageWorkItem?> FindPackageByName(string packageName, string? repo = null)
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

        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        if (workItems.Count == 0)
        {
            return null;
        }

        var packages = workItems.Select(WorkItemMappers.MapToPackageWorkItem).ToList();
        var latest = WorkItemMappers.GetLatestPackageVersions(packages);
        return latest.FirstOrDefault();
    }

    public async Task<LabelWorkItem?> FindLabelByName(string labelName)
    {
        var escapedLabel = labelName.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'release' AND [System.WorkItemType] = 'Label' AND [Custom.Label] = '{escapedLabel}'";
        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
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
    private async Task<(List<PackageWorkItem> Packages, List<LabelOwnerWorkItem> LabelOwners)> FetchRelatedPackagesAndLabelOwners(IEnumerable<int> relatedIds, string? repo = null)
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
        var escapedPath = string.IsNullOrEmpty(path) ? string.Empty : path.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [System.WorkItemType] = 'Label Owner' AND [Custom.RepoPath] = '{escapedPath}'";
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
            ExpandTeamOwners(lo.Owners);
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

    public async Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(
        string repo,
        OwnerType ownerType,
        string? repoPath,
        LabelWorkItem[] labelWorkItems
    ) {
        var labelTypeString = ownerType.ToWorkItemString();
        var normalizedPath = repoPath ?? string.Empty;

        var escapedRepo = repo.Replace("'", "''");
        var escapedLabelType = labelTypeString.Replace("'", "''");
        var escapedPath = normalizedPath.Replace("'", "''");

        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    $" AND [System.WorkItemType] = 'Label Owner'" +
                    $" AND [Custom.Repository] = '{escapedRepo}'" +
                    $" AND [Custom.LabelType] = '{escapedLabelType}'" +
                    $" AND [Custom.RepoPath] = '{escapedPath}'";

        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        if (workItems.Count > 0)
        {
            var candidates = workItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
            await HydrateLabelOwners(candidates);

            var expectedLabelIds = labelWorkItems.Select(l => l.WorkItemId).ToHashSet();
            foreach (var candidate in candidates)
            {
                var candidateLabelIds = candidate.Labels.Select(l => l.WorkItemId).ToHashSet();
                if (expectedLabelIds.SetEquals(candidateLabelIds))
                {
                    return candidate;
                }
            }
        }

        // No exact match found — create new LabelOwner
        var labelNames = string.Join(", ", labelWorkItems.Select(l => l.LabelName));

        // TODO: Convert repo to "net", "python", etc.
        var title = string.IsNullOrEmpty(normalizedPath)
            ? $"{labelTypeString}: {labelNames}"
            : $"{labelTypeString}: {normalizedPath}";

        var labelOwnerWi = new LabelOwnerWorkItem
        {
            LabelType = labelTypeString,
            Repository = repo,
            RepoPath = normalizedPath
        };
        var created = await devOpsService.CreateWorkItemAsync(labelOwnerWi, "Label Owner", title);
        return WorkItemMappers.MapToLabelOwnerWorkItem(created);
    }

    // ========================
    // Add scenarios
    // ========================

    public async Task<CodeownersModifyResponse> AddOwnersToPackage(
        OwnerWorkItem[] owners,
        string packageName,
        string repo
    ) {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        foreach (var owner in owners)
        {
            if (packageWi.RelatedIds.Contains(owner.WorkItemId))
            {
                logger.LogInformation("Skipped adding @{GitHubAlias}, already a package owner for '{PackageName}'.", owner.GitHubAlias, packageName);
                continue;
            }

            await devOpsService.CreateWorkItemRelationAsync(packageWi.WorkItemId, "related", owner.WorkItemId);
            logger.LogInformation("Added @{GitHubAlias} to package '{PackageName}'.", owner.GitHubAlias, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> AddLabelsToPackage(LabelWorkItem[] labels, string packageName, string repo)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        foreach (var label in labels)
        {
            if (packageWi.RelatedIds.Contains(label.WorkItemId))
            {
                logger.LogInformation("Skipped adding label '{LabelName}', already linked to package '{PackageName}'.", label.LabelName, packageName);
                continue;
            }

            await devOpsService.CreateWorkItemRelationAsync(packageWi.WorkItemId, "related", label.WorkItemId);
            logger.LogInformation("Added label '{LabelName}' to package '{PackageName}'.", label.LabelName, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> AddOwnersAndLabelsToPath(
        OwnerWorkItem[] owners,
        LabelWorkItem[] labels,
        string repo,
        string path,
        OwnerType ownerType
    ) {
        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, ownerType, path, labels);

        foreach (var labelWi in labels)
        {
            if (!labelOwnerWi.RelatedIds.Contains(labelWi.WorkItemId))
            {
                await devOpsService.CreateWorkItemRelationAsync(labelOwnerWi.WorkItemId, "related", labelWi.WorkItemId);
            }
        }

        var labelNames = string.Join("', '", labels.Select(l => l.LabelName));

        foreach (var ownerWorkItem in owners)
        {
            if (labelOwnerWi.RelatedIds.Contains(ownerWorkItem.WorkItemId))
            {
                logger.LogInformation("Skipped adding @{GitHubAlias}, already linked as owner for label(s) '{LabelNames}' and path '{Path}'.", ownerWorkItem.GitHubAlias, labelNames, path);
                continue;
            }

            await devOpsService.CreateWorkItemRelationAsync(labelOwnerWi.WorkItemId, "related", ownerWorkItem.WorkItemId);
            logger.LogInformation("Added @{GitHubAlias} and label(s) '{LabelNames}' to path '{Path}'.", ownerWorkItem.GitHubAlias, labelNames, path);
        }

        return new CodeownersModifyResponse
        {
            View = string.IsNullOrEmpty(path)
                ? await GetViewByLabel(labels.Select(l => l.LabelName).ToArray(), repo)
                : await GetViewByPath(path, repo)
        };
    }

    // ========================
    // Remove scenarios
    // ========================

    public async Task<CodeownersModifyResponse> RemoveOwnersFromPackage(
        OwnerWorkItem[] owners,
        string packageName,
        string repo
    ) {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        foreach (var ownerWi in owners)
        {
            if (!packageWi.RelatedIds.Contains(ownerWi.WorkItemId))
            {
                logger.LogInformation("Skipped removing @{GitHubAlias}, not linked to package '{PackageName}'.", ownerWi.GitHubAlias, packageName);
                continue;
            }

            await devOpsService.RemoveWorkItemRelationAsync(packageWi.WorkItemId, "related", ownerWi.WorkItemId);
            logger.LogInformation("Removed @{GitHubAlias} from package '{PackageName}'.", ownerWi.GitHubAlias, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveLabelsFromPackage(
        LabelWorkItem[] labels,
        string packageName,
        string repo
    ) {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        foreach (var labelWi in labels)
        {
            if (!packageWi.RelatedIds.Contains(labelWi.WorkItemId))
            {
                logger.LogInformation("Skipped removing label '{LabelName}', not linked to package '{PackageName}'.", labelWi.LabelName, packageName);
                continue;
            }
            await devOpsService.RemoveWorkItemRelationAsync(packageWi.WorkItemId, "related", labelWi.WorkItemId);
            logger.LogInformation("Removed label '{LabelName}' from package '{PackageName}'.", labelWi.LabelName, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveOwnersFromLabelsAndPath(
        OwnerWorkItem[] owners,
        LabelWorkItem[] labels,
        string repo,
        string path,
        OwnerType ownerType
    ) {
        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        if (labelOwners.Count == 0)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label Owner work item found for path '{path}'." };
        }
        await HydrateLabelOwners(labelOwners);

        var labelOwner = labelOwners.Single(lo =>
            lo.LabelType.Equals(ownerType.ToWorkItemString(), StringComparison.OrdinalIgnoreCase)
            && lo.Labels.Select(l => l.WorkItemId).ToHashSet().SetEquals(labels.Select(l => l.WorkItemId))
        );

        var labelNames = string.Join("', '", labels.Select(l => l.LabelName));
        foreach (var owner in owners) {
            if (!labelOwner.RelatedIds.Contains(owner.WorkItemId))
            {
                logger.LogInformation("Skipped removing @{GitHubAlias}, not linked as owner for label(s) '{LabelNames}' and path '{Path}'.", owner.GitHubAlias, labelNames, path);
                continue;
            }
            await devOpsService.RemoveWorkItemRelationAsync(labelOwner.WorkItemId, "related", owner.WorkItemId);
            logger.LogInformation("Removed @{GitHubAlias} from owner of label(s) '{LabelNames}' and path '{Path}'.", owner.GitHubAlias, labelNames, path);
        }

        return new CodeownersModifyResponse
        {
            View = string.IsNullOrEmpty(path)
                ? await GetViewByLabel(labels.Select(l => l.LabelName).ToArray(), repo)
                : await GetViewByPath(path, repo)
        };
    }

    // ========================
    // Release gate
    // ========================

    public async Task<ReleaseGateResult> CheckReleaseGateAsync(
        string packageName,
        string repo,
        string packageDirectory,
        CancellationToken ct = default
    ) {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new ReleaseGateResult
            {
                Passed = false,
                ExitCode = 1,
                Message = $"No Package work item found for '{packageName}' in repo '{repo}'.",
                UniqueOwnerCount = 0
            };
        }

        await HydratePackages([packageWi]);

        var packageOwners = CollectUniqueOwners(packageWi.Owners);
        if (packageOwners.Count >= 2)
        {
            return new ReleaseGateResult
            {
                Passed = true,
                ExitCode = 0,
                Message = $"Package '{packageName}' has {packageOwners.Count} unique owner(s). Release gate passed.",
                UniqueOwnerCount = packageOwners.Count
            };
        }

        // Package has < 2 owners, check LabelOwners
        var labelOwners = await QueryLabelOwnersByRepo(repo);
        await HydrateLabelOwners(labelOwners);

        // Filter to LabelOwners with non-empty RepoPath and acceptable LabelType
        var pathBasedLabelOwners = labelOwners
            .Where(lo => !string.IsNullOrEmpty(lo.RepoPath))
            .Where(lo => string.IsNullOrEmpty(lo.LabelType)
                || lo.LabelType.Equals("PR Label", StringComparison.OrdinalIgnoreCase))
            .OrderBy(lo => lo.RepoPath, StringComparer.Ordinal)
            .ToList();

        // Find all LabelOwners whose RepoPath glob-matches the package directory
        var matchingLabelOwners = pathBasedLabelOwners
            .Where(lo => DirectoryUtils.PathExpressionMatchesTargetPath(lo.RepoPath, packageDirectory))
            .ToList();

        if (matchingLabelOwners.Count == 0)
        {
            return new ReleaseGateResult
            {
                Passed = false,
                ExitCode = 1,
                Message = $"Package '{packageName}' has {packageOwners.Count} unique owner(s) and no matching LabelOwner found for path '{packageDirectory}'. Release gate failed.",
                UniqueOwnerCount = packageOwners.Count
            };
        }

        // Collect all unique owners from the package and all matching LabelOwners
        var allOwners = new HashSet<string>(packageOwners, StringComparer.OrdinalIgnoreCase);
        foreach (var lo in matchingLabelOwners)
        {
            foreach (var alias in CollectUniqueOwners(lo.Owners))
            {
                allOwners.Add(alias);
            }
        }

        if (allOwners.Count >= 2)
        {
            return new ReleaseGateResult
            {
                Passed = true,
                ExitCode = 0,
                Message = $"Package '{packageName}' has {allOwners.Count} unique owner(s) (including LabelOwner matches). Release gate passed.",
                UniqueOwnerCount = allOwners.Count
            };
        }

        return new ReleaseGateResult
        {
            Passed = false,
            ExitCode = 1,
            Message = $"Package '{packageName}' has only {allOwners.Count} unique owner(s) (including LabelOwner matches). At least 2 are required. Release gate failed.",
            UniqueOwnerCount = allOwners.Count
        };
    }

    /// <summary>
    /// Queries all LabelOwner work items for a given repository.
    /// </summary>
    internal async Task<List<LabelOwnerWorkItem>> QueryLabelOwnersByRepo(string repo)
    {
        var escapedRepo = repo.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    $" AND [System.WorkItemType] = 'Label Owner'" +
                    $" AND [Custom.Repository] = '{escapedRepo}'" +
                    $" AND [Custom.RepoPath] <> ''";
        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        return rawWorkItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
    }

    /// <summary>
    /// Collects unique owner aliases from a list of OwnerWorkItems, expanding teams to individual members.
    /// </summary>
    private static HashSet<string> CollectUniqueOwners(List<OwnerWorkItem> owners)
    {
        var uniqueOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in owners)
        {
            if (owner.IsGitHubTeam && owner.ExpandedMembers.Count > 0)
            {
                foreach (var member in owner.ExpandedMembers)
                {
                    uniqueOwners.Add(member);
                }
            }
            else
            {
                uniqueOwners.Add(owner.GitHubAlias);
            }
        }
        return uniqueOwners;
    }

}
