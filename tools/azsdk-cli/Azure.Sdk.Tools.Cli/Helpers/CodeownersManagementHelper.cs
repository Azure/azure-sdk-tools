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
    ITeamUserCache teamUserCache,
    ICodeownersValidatorHelper validatorHelper
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

    private async Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias)
    {
        var escapedAlias = alias.Replace("'", "''");
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

    private async Task<LabelWorkItem?> FindLabelByName(string labelName)
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
        var escapedPath = path.Replace("'", "''");
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

    // ========================
    // Owner type mapping
    // ========================

    /// <summary>Maps CLI owner-type values to work item field values.</summary>
    public static string MapOwnerType(string ownerType) => ownerType.ToLowerInvariant() switch
    {
        "service-owner" => "Service Owner",
        "azsdk-owner"   => "Azure SDK Owner",
        "pr-label"      => "PR Label",
        _ => throw new ArgumentException($"Invalid owner type '{ownerType}'. Valid values are: service-owner, azsdk-owner, pr-label.")
    };

    // ========================
    // Find-or-create helpers
    // ========================

    public async Task<OwnerWorkItem> FindOrCreateOwnerAsync(string gitHubAlias)
    {
        var alias = NormalizeGitHubAlias(gitHubAlias);

        var existing = await FindOwnerByGitHubAlias(alias);
        if (existing != null)
        {
            return existing;
        }

        // Validate before creating
        var validation = await validatorHelper.ValidateCodeOwnerAsync(alias, verbose: false);
        if (!validation.IsValidCodeOwner)
        {
            throw new InvalidOperationException(
                $"GitHub user '{alias}' is not a valid Azure SDK code owner: {validation.Message}");
        }

        var ownerWi = new OwnerWorkItem { GitHubAlias = alias };
        var created = await devOpsService.CreateWorkItemWithFieldsAsync(ownerWi, "Owner", alias);
        return WorkItemMappers.MapToOwnerWorkItem(created);
    }

    public async Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(string repo, string ownerType, string? repoPath, string label)
    {
        var labelTypeString = MapOwnerType(ownerType);
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
            return WorkItemMappers.MapToLabelOwnerWorkItem(workItems.First());
        }

        // Create new LabelOwner
        var title = string.IsNullOrEmpty(normalizedPath)
            ? $"{labelTypeString}: {label}"
            : $"{labelTypeString}: {normalizedPath}";

        var labelOwnerWi = new LabelOwnerWorkItem
        {
            LabelType = labelTypeString,
            Repository = repo,
            RepoPath = normalizedPath
        };
        var created = await devOpsService.CreateWorkItemWithFieldsAsync(labelOwnerWi, "Label Owner", title);
        return WorkItemMappers.MapToLabelOwnerWorkItem(created);
    }

    // ========================
    // Add scenarios
    // ========================

    public async Task<CodeownersModifyResponse> AddOwnerToPackageAsync(string ownerAlias, string packageName, string repo)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        OwnerWorkItem ownerWi;
        try
        {
            ownerWi = await FindOrCreateOwnerAsync(ownerAlias);
        }
        catch (InvalidOperationException ex)
        {
            return new CodeownersModifyResponse { ResponseError = ex.Message };
        }

        if (packageWi.RelatedIds.Contains(ownerWi.WorkItemId))
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Owner '@{ownerWi.GitHubAlias}' is already linked to package '{packageName}'."
            };
        }

        await devOpsService.CreateRelatedLinkAsync(packageWi.WorkItemId, ownerWi.WorkItemId);

        return new CodeownersModifyResponse
        {
            Operation = $"Added @{ownerWi.GitHubAlias} to package '{packageName}'.",
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> AddLabelToPackageAsync(string label, string packageName, string repo)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        if (packageWi.RelatedIds.Contains(labelWi.WorkItemId))
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Label '{label}' is already linked to package '{packageName}'."
            };
        }

        await devOpsService.CreateRelatedLinkAsync(packageWi.WorkItemId, labelWi.WorkItemId);

        return new CodeownersModifyResponse
        {
            Operation = $"Added label '{label}' to package '{packageName}'.",
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> AddOwnerToLabelAsync(string ownerAlias, string label, string repo, string ownerType)
    {
        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        OwnerWorkItem ownerWi;
        try
        {
            ownerWi = await FindOrCreateOwnerAsync(ownerAlias);
        }
        catch (InvalidOperationException ex)
        {
            return new CodeownersModifyResponse { ResponseError = ex.Message };
        }

        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, ownerType, null, label);

        if (!labelOwnerWi.RelatedIds.Contains(ownerWi.WorkItemId))
        {
            await devOpsService.CreateRelatedLinkAsync(labelOwnerWi.WorkItemId, ownerWi.WorkItemId);
        }

        if (!labelOwnerWi.RelatedIds.Contains(labelWi.WorkItemId))
        {
            await devOpsService.CreateRelatedLinkAsync(labelOwnerWi.WorkItemId, labelWi.WorkItemId);
        }

        return new CodeownersModifyResponse
        {
            Operation = $"Added @{ownerWi.GitHubAlias} as {MapOwnerType(ownerType)} for label '{label}'.",
            View = await GetViewByLabel([label], repo)
        };
    }

    public async Task<CodeownersModifyResponse> AddOwnerAndLabelToPathAsync(string ownerAlias, string label, string repo, string path, string ownerType)
    {
        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        OwnerWorkItem ownerWi;
        try
        {
            ownerWi = await FindOrCreateOwnerAsync(ownerAlias);
        }
        catch (InvalidOperationException ex)
        {
            return new CodeownersModifyResponse { ResponseError = ex.Message };
        }

        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, ownerType, path, label);

        if (!labelOwnerWi.RelatedIds.Contains(ownerWi.WorkItemId))
        {
            await devOpsService.CreateRelatedLinkAsync(labelOwnerWi.WorkItemId, ownerWi.WorkItemId);
        }

        if (!labelOwnerWi.RelatedIds.Contains(labelWi.WorkItemId))
        {
            await devOpsService.CreateRelatedLinkAsync(labelOwnerWi.WorkItemId, labelWi.WorkItemId);
        }

        return new CodeownersModifyResponse
        {
            Operation = $"Added @{ownerWi.GitHubAlias} and label '{label}' to path '{path}'.",
            View = await GetViewByPath(path, repo)
        };
    }

    // ========================
    // Remove scenarios
    // ========================

    public async Task<CodeownersModifyResponse> RemoveOwnerFromPackageAsync(string ownerAlias, string packageName, string repo)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        var alias = NormalizeGitHubAlias(ownerAlias);
        var ownerWi = await FindOwnerByGitHubAlias(alias);
        if (ownerWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Owner work item found for alias '{alias}'." };
        }

        if (!packageWi.RelatedIds.Contains(ownerWi.WorkItemId))
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Owner '@{alias}' is not linked to package '{packageName}'."
            };
        }

        await devOpsService.RemoveRelatedLinkAsync(packageWi.WorkItemId, ownerWi.WorkItemId);

        return new CodeownersModifyResponse
        {
            Operation = $"Removed @{alias} from package '{packageName}'.",
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveLabelFromPackageAsync(string label, string packageName, string repo)
    {
        var packageWi = await FindPackageByName(packageName, repo);
        if (packageWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Package work item found for '{packageName}'." };
        }

        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        if (!packageWi.RelatedIds.Contains(labelWi.WorkItemId))
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Label '{label}' is not linked to package '{packageName}'."
            };
        }

        await devOpsService.RemoveRelatedLinkAsync(packageWi.WorkItemId, labelWi.WorkItemId);

        return new CodeownersModifyResponse
        {
            Operation = $"Removed label '{label}' from package '{packageName}'.",
            View = await GetViewByPackage(packageName, repo)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveOwnerFromLabelAsync(string ownerAlias, string label, string repo, string ownerType)
    {
        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        var alias = NormalizeGitHubAlias(ownerAlias);
        var ownerWi = await FindOwnerByGitHubAlias(alias);
        if (ownerWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Owner work item found for alias '{alias}'." };
        }

        var labelTypeString = MapOwnerType(ownerType);
        var escapedRepo = repo.Replace("'", "''");
        var escapedLabelType = labelTypeString.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    $" AND [System.WorkItemType] = 'Label Owner'" +
                    $" AND [Custom.Repository] = '{escapedRepo}'" +
                    $" AND [Custom.LabelType] = '{escapedLabelType}'" +
                    $" AND [Custom.RepoPath] = ''";

        var rawWorkItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
        var labelOwnerWi = rawWorkItems
            .Select(WorkItemMappers.MapToLabelOwnerWorkItem)
            .FirstOrDefault(lo => lo.RelatedIds.Contains(ownerWi.WorkItemId));

        if (labelOwnerWi == null)
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Owner '@{alias}' is not linked as {labelTypeString} for label '{label}' in repo '{repo}'."
            };
        }

        await devOpsService.RemoveRelatedLinkAsync(labelOwnerWi.WorkItemId, ownerWi.WorkItemId);

        return new CodeownersModifyResponse
        {
            Operation = $"Removed @{alias} as {labelTypeString} for label '{label}'.",
            View = await GetViewByLabel([label], repo)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveOwnerAndLabelFromPathAsync(string ownerAlias, string label, string repo, string path, string ownerType)
    {
        var labelWi = await FindLabelByName(label);
        if (labelWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label work item found for '{label}'." };
        }

        var alias = NormalizeGitHubAlias(ownerAlias);
        var ownerWi = await FindOwnerByGitHubAlias(alias);
        if (ownerWi == null)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Owner work item found for alias '{alias}'." };
        }

        var labelOwners = await QueryLabelOwnersByPath(path, repo);
        var labelOwnerWi = labelOwners.FirstOrDefault(lo => lo.RelatedIds.Contains(ownerWi.WorkItemId));

        if (labelOwnerWi == null)
        {
            return new CodeownersModifyResponse
            {
                ResponseError = $"Owner '@{alias}' is not linked to path '{path}' in repo '{repo}'."
            };
        }

        if (labelOwnerWi.RelatedIds.Contains(ownerWi.WorkItemId))
        {
            await devOpsService.RemoveRelatedLinkAsync(labelOwnerWi.WorkItemId, ownerWi.WorkItemId);
        }

        if (labelOwnerWi.RelatedIds.Contains(labelWi.WorkItemId))
        {
            await devOpsService.RemoveRelatedLinkAsync(labelOwnerWi.WorkItemId, labelWi.WorkItemId);
        }

        return new CodeownersModifyResponse
        {
            Operation = $"Removed @{alias} and label '{label}' from path '{path}'.",
            View = await GetViewByPath(path, repo)
        };
    }

}
