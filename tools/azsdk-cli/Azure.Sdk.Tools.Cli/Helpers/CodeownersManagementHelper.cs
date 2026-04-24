// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Azure.Sdk.Tools.Cli.Configuration;
using Octokit;

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
    ITeamUserCache teamUserCache,
    IGitHubService githubService
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
        var labelOwners = await QueryLabelOwnersByPath(path, repo, null, ct);
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

    public async Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias, CancellationToken ct)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);
        var escapedAlias = normalizedAlias.Replace("'", "''");
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

    public async Task<LabelWorkItem?> FindLabelByName(string labelName, CancellationToken ct)
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

    private async Task<List<LabelOwnerWorkItem>> QueryLabelOwnersByPath(string path, string? repo, string? section, CancellationToken ct)
    {
        var escapedPath = string.IsNullOrEmpty(path) ? string.Empty : path.Replace("'", "''");
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}' AND [System.WorkItemType] = 'Label Owner' AND [Custom.RepoPath] = '{escapedPath}'";
        if (!string.IsNullOrEmpty(repo))
        {
            var escapedRepo = repo.Replace("'", "''");
            query += $" AND [Custom.Repository] = '{escapedRepo}'";
        }
        if (!string.IsNullOrEmpty(section))
        {
            var escapedSection = section.Replace("'", "''");
            query += $" AND [Custom.Section] = '{escapedSection}'";
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

    public async Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(
        string repo,
        OwnerType ownerType,
        string? repoPath,
        LabelWorkItem[] labelWorkItems,
        string section,
        CancellationToken ct
    ) {
        var labelTypeString = ownerType.ToWorkItemString();
        var normalizedPath = repoPath ?? string.Empty;

        var escapedRepo = repo.Replace("'", "''");
        var escapedLabelType = labelTypeString.Replace("'", "''");
        var escapedPath = normalizedPath.Replace("'", "''");
        var escapedSection = section.Replace("'", "''");

        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    $" AND [System.WorkItemType] = 'Label Owner'" +
                    $" AND [Custom.Repository] = '{escapedRepo}'" +
                    $" AND [Custom.LabelType] = '{escapedLabelType}'" +
                    $" AND [Custom.RepoPath] = '{escapedPath}'" +
                    $" AND [Custom.Section] = '{escapedSection}'";

        var workItems = await devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations, ct: ct);
        if (workItems.Count > 0)
        {
            var candidates = workItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
            await HydrateLabelOwners(candidates, ct);

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
            RepoPath = normalizedPath,
            Section = section
        };
        var created = await devOpsService.CreateWorkItemAsync(labelOwnerWi, "Label Owner", title, ct: ct);
        return WorkItemMappers.MapToLabelOwnerWorkItem(created);
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the team alias is not in the expected
    /// format or does not descend from the azure-sdk-write team.
    /// </summary>
    public async Task ThrowIfInvalidTeamAlias(string alias, CancellationToken ct)
    {
        var normalizedAlias = NormalizeGitHubAlias(alias);

        if (normalizedAlias.Count(c => c == '/') != 1)
        {
            throw new InvalidOperationException(
                $"Invalid team alias '{alias}'. Team aliases must be in the format '<org>/<team>' with exactly one '/'.");
        }

        // Extract the team name without the org prefix for cache lookup
        var parts = normalizedAlias.Split('/', StringSplitOptions.TrimEntries);
        var org = parts[0];
        var teamSlug = parts[1];

        if (string.IsNullOrEmpty(org) || string.IsNullOrEmpty(teamSlug))
        {
            throw new InvalidOperationException(
                $"Invalid team alias '{alias}'. Both the organization and team name must be non-empty.");
        }

        if (!string.Equals(org, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid team alias '{alias}'. Only teams in the 'Azure' organization are supported.");
        }

        // Check if the team exists in the TeamUserDict (populated from azure-sdk-write blob).
        // Use ContainsKey rather than GetUsersForTeam so that teams with zero members
        // are still accepted — empty-team validation happens later during the release gate.
        if (teamUserCache.TeamUserDict.ContainsKey(teamSlug))
        {
            logger.LogInformation("Team '{Alias}' found in TeamUserCache", alias);
            return;
        }

        logger.LogInformation("Team '{Alias}' not found in TeamUserCache, verifying via GitHub API", alias);

        // Fetch the team from GitHub
        var team = await githubService.GetTeamByNameAsync(org, teamSlug, ct);

        // Walk the parent chain to verify the team is under azure-sdk-write.
        // The loop is bounded by MaxParentDepth to prevent infinite loops.
        const int MaxParentDepth = 20;
        const string AzureSdkWriteTeamSlug = "azure-sdk-write";

        var current = team;
        for (int depth = 0; depth < MaxParentDepth; depth++)
        {
            if (string.Equals(current.Slug, AzureSdkWriteTeamSlug, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Team '{Alias}' is a descendant of '{Parent}'", alias, AzureSdkWriteTeamSlug);
                return;
            }

            if (current.Parent == null)
            {
                break;
            }

            // Re-fetch the parent team to get the complete object with its own Parent populated.
            current = await githubService.GetTeamByNameAsync(org, current.Parent.Slug, ct);
        }

        throw new InvalidOperationException(
            $"GitHub team '{alias}' is not a child of '{AzureSdkWriteTeamSlug}'. " +
            $"Only teams that descend from '{AzureSdkWriteTeamSlug}' can be added as CODEOWNERS.");
    }

    // ========================
    // Add scenarios
    // ========================

    public async Task<CodeownersModifyResponse> AddOwnersToPackage(
        OwnerWorkItem[] owners,
        string packageName,
        string repo,
        CancellationToken ct
    ) {
        var packageWi = await FindPackageByName(packageName, repo, ct);
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

            await devOpsService.CreateWorkItemRelationAsync(packageWi.WorkItemId, "related", owner.WorkItemId, ct: ct);
            logger.LogInformation("Added @{GitHubAlias} to package '{PackageName}'.", owner.GitHubAlias, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo, ct)
        };
    }

    public async Task<CodeownersModifyResponse> AddLabelsToPackage(LabelWorkItem[] labels, string packageName, string repo, CancellationToken ct)
    {
        var packageWi = await FindPackageByName(packageName, repo, ct);
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

            await devOpsService.CreateWorkItemRelationAsync(packageWi.WorkItemId, "related", label.WorkItemId, ct: ct);
            logger.LogInformation("Added label '{LabelName}' to package '{PackageName}'.", label.LabelName, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo, ct)
        };
    }

    public async Task<CodeownersModifyResponse> AddOwnersAndLabelsToPath(
        OwnerWorkItem[] owners,
        LabelWorkItem[] labels,
        string repo,
        string path,
        OwnerType ownerType,
        string section,
        CancellationToken ct
    ) {
        var labelOwnerWi = await FindOrCreateLabelOwnerAsync(repo, ownerType, path, labels, section, ct);

        foreach (var labelWi in labels)
        {
            if (!labelOwnerWi.RelatedIds.Contains(labelWi.WorkItemId))
            {
                await devOpsService.CreateWorkItemRelationAsync(labelOwnerWi.WorkItemId, "related", labelWi.WorkItemId, ct: ct);
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

            await devOpsService.CreateWorkItemRelationAsync(labelOwnerWi.WorkItemId, "related", ownerWorkItem.WorkItemId, ct: ct);
            logger.LogInformation("Added @{GitHubAlias} and label(s) '{LabelNames}' to path '{Path}'.", ownerWorkItem.GitHubAlias, labelNames, path);
        }

        return new CodeownersModifyResponse
        {
            View = string.IsNullOrEmpty(path)
                ? await GetViewByLabel(labels.Select(l => l.LabelName).ToArray(), repo, ct)
                : await GetViewByPath(path, repo, ct)
        };
    }

    // ========================
    // Remove scenarios
    // ========================

    public async Task<CodeownersModifyResponse> RemoveOwnersFromPackage(
        OwnerWorkItem[] owners,
        string packageName,
        string repo,
        CancellationToken ct
    ) {
        var packageWi = await FindPackageByName(packageName, repo, ct);
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

            await devOpsService.RemoveWorkItemRelationAsync(packageWi.WorkItemId, "related", ownerWi.WorkItemId, ct);
            logger.LogInformation("Removed @{GitHubAlias} from package '{PackageName}'.", ownerWi.GitHubAlias, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo, ct)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveLabelsFromPackage(
        LabelWorkItem[] labels,
        string packageName,
        string repo,
        CancellationToken ct
    ) {
        var packageWi = await FindPackageByName(packageName, repo, ct);
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
            await devOpsService.RemoveWorkItemRelationAsync(packageWi.WorkItemId, "related", labelWi.WorkItemId, ct);
            logger.LogInformation("Removed label '{LabelName}' from package '{PackageName}'.", labelWi.LabelName, packageName);
        }

        return new CodeownersModifyResponse
        {
            View = await GetViewByPackage(packageName, repo, ct)
        };
    }

    public async Task<CodeownersModifyResponse> RemoveOwnersFromLabelsAndPath(
        OwnerWorkItem[] owners,
        LabelWorkItem[] labels,
        string repo,
        string path,
        OwnerType ownerType,
        string section,
        CancellationToken ct
    ) {
        var labelOwners = await QueryLabelOwnersByPath(path, repo, section, ct);
        if (labelOwners.Count == 0)
        {
            return new CodeownersModifyResponse { ResponseError = $"No Label Owner work item found for path '{path}'." };
        }
        await HydrateLabelOwners(labelOwners, ct);

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
            await devOpsService.RemoveWorkItemRelationAsync(labelOwner.WorkItemId, "related", owner.WorkItemId, ct);
            logger.LogInformation("Removed @{GitHubAlias} from owner of label(s) '{LabelNames}' and path '{Path}'.", owner.GitHubAlias, labelNames, path);
        }

        return new CodeownersModifyResponse
        {
            View = string.IsNullOrEmpty(path)
                ? await GetViewByLabel(labels.Select(l => l.LabelName).ToArray(), repo, ct)
                : await GetViewByPath(path, repo, ct)
        };
    }

}
