// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class CodeownersManagementHelper : ICodeownersManagementHelper
{
    private readonly IDevOpsService _devOpsService;
    private readonly ICodeownersValidatorHelper _validatorHelper;
    private readonly ILogger<CodeownersManagementHelper> _logger;

    public CodeownersManagementHelper(
        IDevOpsService devOpsService,
        ICodeownersValidatorHelper validatorHelper,
        ILogger<CodeownersManagementHelper> logger)
    {
        _devOpsService = devOpsService;
        _validatorHelper = validatorHelper;
        _logger = logger;
    }

    #region View Methods

    public async Task<CodeownersViewResult> GetViewByUserAsync(string alias, string? repo)
    {
        var owner = await _devOpsService.GetOwnerByGitHubAliasAsync(alias);
        if (owner == null)
        {
            return new CodeownersViewResult { Message = $"No Owner work item found for GitHub alias '{alias}'." };
        }

        var allPackages = await GetPackagesRelatedToOwnerAsync(owner.WorkItemId);
        var labelOwners = await GetLabelOwnersRelatedToOwnerAsync(owner.WorkItemId, repo);

        return BuildViewResult(allPackages, labelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByLabelAsync(string label, string? repo)
    {
        var labelWi = await _devOpsService.GetLabelByNameAsync(label);
        if (labelWi == null)
        {
            return new CodeownersViewResult { Message = $"No Label work item found for '{label}'." };
        }

        var packages = await GetPackagesRelatedToLabelAsync(labelWi.WorkItemId);
        var labelOwners = await GetLabelOwnersRelatedToLabelAsync(labelWi.WorkItemId, repo);

        return BuildViewResult(packages, labelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPathAsync(string path, string? repo)
    {
        List<LabelOwnerWorkItem> labelOwners;
        if (!string.IsNullOrEmpty(repo))
        {
            labelOwners = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, path);
        }
        else
        {
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                        $" AND [System.WorkItemType] = 'Label Owner'" +
                        $" AND [Custom.RepoPath] = '{path.Replace("'", "''")}'";
            var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
            labelOwners = workItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
        }

        if (labelOwners.Count == 0)
        {
            return new CodeownersViewResult { Message = $"No Label Owner work items found for path '{path}'." };
        }

        await HydrateLabelOwnersAsync(labelOwners);

        return BuildViewResult([], labelOwners);
    }

    public async Task<CodeownersViewResult> GetViewByPackageAsync(string packageName)
    {
        var package = await _devOpsService.GetPackageByNameAsync(packageName);
        if (package == null)
        {
            return new CodeownersViewResult { Message = $"No Package work item found for '{packageName}'." };
        }

        await HydratePackageAsync(package);

        var packages = new List<PackageWorkItem> { package };
        var labelOwners = package.LabelOwners.ToList();

        return BuildViewResult(packages, labelOwners);
    }

    #endregion

    #region Add Methods

    public async Task<CodeownersViewResult> AddOwnerToPackageAsync(string alias, string packageName, string repo)
    {
        var owner = await FindOrCreateOwnerAsync(alias);
        var package = await _devOpsService.GetPackageByNameAsync(packageName);
        if (package == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"Package '{packageName}' not found.",
                Message = $"Package '{packageName}' not found. Please verify the package name."
            };
        }

        await _devOpsService.AddRelatedLinkAsync(owner.WorkItemId, package.WorkItemId);

        return new CodeownersViewResult
        {
            Message = $"Added '{alias}' as source owner of package '{packageName}'.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> AddOwnerToLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path)
    {
        var owner = await FindOrCreateOwnerAsync(alias);

        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await _devOpsService.GetLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                return new CodeownersViewResult
                {
                    ResponseError = $"Label '{labelName}' not found. Labels cannot be auto-created.",
                    Message = $"Label '{labelName}' not found. Labels are centrally managed and cannot be auto-created."
                };
            }
            labelWorkItems.Add(labelWi);
        }

        var repoPath = path ?? string.Empty;
        var labelOwner = await FindOrCreateLabelOwnerAsync(repo, ownerType, repoPath, labels);

        await _devOpsService.AddRelatedLinkAsync(owner.WorkItemId, labelOwner.WorkItemId);

        foreach (var labelWi in labelWorkItems)
        {
            await _devOpsService.AddRelatedLinkAsync(labelWi.WorkItemId, labelOwner.WorkItemId);
        }

        return new CodeownersViewResult
        {
            Message = $"Added '{alias}' as {ownerType} for labels [{string.Join(", ", labels)}] in {repo}.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> AddOwnerToPathAsync(string alias, string repo, string path, string ownerType)
    {
        var owner = await FindOrCreateOwnerAsync(alias);

        var labelOwner = await FindOrCreateLabelOwnerAsync(repo, ownerType, path, []);

        await _devOpsService.AddRelatedLinkAsync(owner.WorkItemId, labelOwner.WorkItemId);

        return new CodeownersViewResult
        {
            Message = $"Added '{alias}' as {ownerType} for path '{path}' in {repo}.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> AddLabelToPathAsync(List<string> labels, string repo, string path)
    {
        var labelWorkItems = new List<LabelWorkItem>();
        foreach (var labelName in labels)
        {
            var labelWi = await _devOpsService.GetLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                return new CodeownersViewResult
                {
                    ResponseError = $"Label '{labelName}' not found. Labels cannot be auto-created.",
                    Message = $"Label '{labelName}' not found. Labels are centrally managed and cannot be auto-created."
                };
            }
            labelWorkItems.Add(labelWi);
        }

        var existingLabelOwners = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, path);
        LabelOwnerWorkItem labelOwner;
        if (existingLabelOwners.Count > 0)
        {
            labelOwner = existingLabelOwners[0];
        }
        else
        {
            labelOwner = await _devOpsService.CreateLabelOwnerWorkItemAsync(repo, "", path, labels.ToList());
        }

        foreach (var labelWi in labelWorkItems)
        {
            await _devOpsService.AddRelatedLinkAsync(labelWi.WorkItemId, labelOwner.WorkItemId);
        }

        return new CodeownersViewResult
        {
            Message = $"Added labels [{string.Join(", ", labels)}] to path '{path}' in {repo}.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    #endregion

    #region Remove Methods

    public async Task<CodeownersViewResult> RemoveOwnerFromPackageAsync(string alias, string packageName, string repo)
    {
        var owner = await _devOpsService.GetOwnerByGitHubAliasAsync(alias);
        if (owner == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Owner work item found for '{alias}'.",
                Message = $"Owner '{alias}' not found."
            };
        }

        var package = await _devOpsService.GetPackageByNameAsync(packageName);
        if (package == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"Package '{packageName}' not found.",
                Message = $"Package '{packageName}' not found."
            };
        }

        await _devOpsService.RemoveRelatedLinkAsync(owner.WorkItemId, package.WorkItemId);

        return new CodeownersViewResult
        {
            Message = $"Removed '{alias}' as owner of package '{packageName}'.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> RemoveOwnerFromLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path)
    {
        var owner = await _devOpsService.GetOwnerByGitHubAliasAsync(alias);
        if (owner == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Owner work item found for '{alias}'.",
                Message = $"Owner '{alias}' not found."
            };
        }

        var repoPath = path ?? string.Empty;
        var labelOwners = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, repoPath);

        var matchingLabelOwner = labelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(ownerType, StringComparison.OrdinalIgnoreCase));

        if (matchingLabelOwner == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Label Owner found for {ownerType} in {repo} at path '{repoPath}'.",
                Message = $"No matching Label Owner found."
            };
        }

        await _devOpsService.RemoveRelatedLinkAsync(owner.WorkItemId, matchingLabelOwner.WorkItemId);

        await HydrateLabelOwnersAsync([matchingLabelOwner]);
        var remainingOwners = matchingLabelOwner.Owners.Where(o => !o.GitHubAlias.Equals(alias, StringComparison.OrdinalIgnoreCase)).ToList();
        var warningMessage = remainingOwners.Count == 0
            ? " Warning: This Label Owner has no remaining owners."
            : "";

        return new CodeownersViewResult
        {
            Message = $"Removed '{alias}' as {ownerType} for labels [{string.Join(", ", labels)}] in {repo}.{warningMessage}",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> RemoveOwnerFromPathAsync(string alias, string repo, string path, string ownerType)
    {
        var owner = await _devOpsService.GetOwnerByGitHubAliasAsync(alias);
        if (owner == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Owner work item found for '{alias}'.",
                Message = $"Owner '{alias}' not found."
            };
        }

        var labelOwners = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, path);
        var matchingLabelOwner = labelOwners.FirstOrDefault(lo =>
            lo.LabelType.Equals(ownerType, StringComparison.OrdinalIgnoreCase));

        if (matchingLabelOwner == null)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Label Owner found for {ownerType} at path '{path}' in {repo}.",
                Message = $"No matching Label Owner found."
            };
        }

        await _devOpsService.RemoveRelatedLinkAsync(owner.WorkItemId, matchingLabelOwner.WorkItemId);

        return new CodeownersViewResult
        {
            Message = $"Removed '{alias}' as {ownerType} for path '{path}' in {repo}.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    public async Task<CodeownersViewResult> RemoveLabelFromPathAsync(List<string> labels, string repo, string path)
    {
        var labelOwners = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, path);
        if (labelOwners.Count == 0)
        {
            return new CodeownersViewResult
            {
                ResponseError = $"No Label Owner found for path '{path}' in {repo}.",
                Message = $"No matching Label Owner found."
            };
        }

        var labelOwner = labelOwners[0];

        foreach (var labelName in labels)
        {
            var labelWi = await _devOpsService.GetLabelByNameAsync(labelName);
            if (labelWi == null)
            {
                _logger.LogWarning("Label '{LabelName}' not found, skipping", labelName);
                continue;
            }

            await _devOpsService.RemoveRelatedLinkAsync(labelWi.WorkItemId, labelOwner.WorkItemId);
        }

        return new CodeownersViewResult
        {
            Message = $"Removed labels [{string.Join(", ", labels)}] from path '{path}' in {repo}.",
            NextSteps = ["Run 'azsdk config codeowners render' to regenerate the CODEOWNERS file."]
        };
    }

    #endregion

    #region Helper Methods

    private async Task<OwnerWorkItem> FindOrCreateOwnerAsync(string alias)
    {
        var validation = await _validatorHelper.ValidateCodeOwnerAsync(alias);
        if (!validation.IsValidCodeOwner)
        {
            throw new InvalidOperationException($"'{alias}' is not a valid code owner. {validation.Message}");
        }

        return await _devOpsService.CreateOwnerWorkItemAsync(alias);
    }

    private async Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(string repo, string labelType, string repoPath, List<string> labels)
    {
        var existing = await _devOpsService.GetLabelOwnersByRepoAndPathAsync(repo, repoPath);
        var match = existing.FirstOrDefault(lo =>
            lo.LabelType.Equals(labelType, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            return match;
        }

        return await _devOpsService.CreateLabelOwnerWorkItemAsync(repo, labelType, repoPath, labels);
    }

    private async Task<List<PackageWorkItem>> GetPackagesRelatedToOwnerAsync(int ownerId)
    {
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    " AND [System.WorkItemType] = 'Package'";
        var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);

        var packages = workItems
            .Select(WorkItemMappers.MapToPackageWorkItem)
            .Where(p => p.RelatedIds.Contains(ownerId))
            .ToList();

        return WorkItemMappers.GetLatestPackageVersions(packages);
    }

    private async Task<List<LabelOwnerWorkItem>> GetLabelOwnersRelatedToOwnerAsync(int ownerId, string? repo)
    {
        List<LabelOwnerWorkItem> labelOwners;
        if (!string.IsNullOrEmpty(repo))
        {
            labelOwners = await _devOpsService.GetLabelOwnersByRepoAsync(repo);
        }
        else
        {
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                        " AND [System.WorkItemType] = 'Label Owner'";
            var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
            labelOwners = workItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
        }

        var filtered = labelOwners.Where(lo => lo.RelatedIds.Contains(ownerId)).ToList();
        await HydrateLabelOwnersAsync(filtered);
        return filtered;
    }

    private async Task<List<PackageWorkItem>> GetPackagesRelatedToLabelAsync(int labelId)
    {
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                    " AND [System.WorkItemType] = 'Package'";
        var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);

        var packages = workItems
            .Select(WorkItemMappers.MapToPackageWorkItem)
            .Where(p => p.RelatedIds.Contains(labelId))
            .ToList();

        return WorkItemMappers.GetLatestPackageVersions(packages);
    }

    private async Task<List<LabelOwnerWorkItem>> GetLabelOwnersRelatedToLabelAsync(int labelId, string? repo)
    {
        List<LabelOwnerWorkItem> labelOwners;
        if (!string.IsNullOrEmpty(repo))
        {
            labelOwners = await _devOpsService.GetLabelOwnersByRepoAsync(repo);
        }
        else
        {
            var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}'" +
                        " AND [System.WorkItemType] = 'Label Owner'";
            var workItems = await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
            labelOwners = workItems.Select(WorkItemMappers.MapToLabelOwnerWorkItem).ToList();
        }

        var filtered = labelOwners.Where(lo => lo.RelatedIds.Contains(labelId)).ToList();
        await HydrateLabelOwnersAsync(filtered);
        return filtered;
    }

    private async Task HydrateLabelOwnersAsync(List<LabelOwnerWorkItem> labelOwners)
    {
        var allRelatedIds = labelOwners.SelectMany(lo => lo.RelatedIds).Distinct().ToHashSet();
        if (allRelatedIds.Count == 0)
        {
            return;
        }

        var workItemClient = await GetWorkItemsAsync(allRelatedIds.ToList());
        var ownerMap = new Dictionary<int, OwnerWorkItem>();
        var labelMap = new Dictionary<int, LabelWorkItem>();

        foreach (var wi in workItemClient)
        {
            if (wi.Fields.TryGetValue("System.WorkItemType", out var wiType))
            {
                var typeStr = wiType?.ToString() ?? "";
                if (typeStr == "Owner")
                {
                    ownerMap[wi.Id!.Value] = WorkItemMappers.MapToOwnerWorkItem(wi);
                }
                else if (typeStr == "Label")
                {
                    labelMap[wi.Id!.Value] = WorkItemMappers.MapToLabelWorkItem(wi);
                }
            }
        }

        foreach (var lo in labelOwners)
        {
            lo.Owners.Clear();
            lo.Labels.Clear();
            foreach (var id in lo.RelatedIds)
            {
                if (ownerMap.TryGetValue(id, out var owner))
                {
                    lo.Owners.Add(owner);
                }
                else if (labelMap.TryGetValue(id, out var label))
                {
                    lo.Labels.Add(label);
                }
            }
        }
    }

    private async Task HydratePackageAsync(PackageWorkItem package)
    {
        var allRelatedIds = package.RelatedIds.ToList();
        if (allRelatedIds.Count == 0)
        {
            return;
        }

        var workItems = await GetWorkItemsAsync(allRelatedIds);
        var ownerMap = new Dictionary<int, OwnerWorkItem>();
        var labelMap = new Dictionary<int, LabelWorkItem>();
        var labelOwnerMap = new Dictionary<int, LabelOwnerWorkItem>();

        foreach (var wi in workItems)
        {
            if (wi.Fields.TryGetValue("System.WorkItemType", out var wiType))
            {
                var typeStr = wiType?.ToString() ?? "";
                if (typeStr == "Owner")
                {
                    ownerMap[wi.Id!.Value] = WorkItemMappers.MapToOwnerWorkItem(wi);
                }
                else if (typeStr == "Label")
                {
                    labelMap[wi.Id!.Value] = WorkItemMappers.MapToLabelWorkItem(wi);
                }
                else if (typeStr == "Label Owner")
                {
                    labelOwnerMap[wi.Id!.Value] = WorkItemMappers.MapToLabelOwnerWorkItem(wi);
                }
            }
        }

        package.Owners.Clear();
        package.Labels.Clear();
        package.LabelOwners.Clear();
        foreach (var id in package.RelatedIds)
        {
            if (ownerMap.TryGetValue(id, out var owner))
            {
                package.Owners.Add(owner);
            }
            else if (labelMap.TryGetValue(id, out var label))
            {
                package.Labels.Add(label);
            }
            else if (labelOwnerMap.TryGetValue(id, out var labelOwner))
            {
                package.LabelOwners.Add(labelOwner);
            }
        }

        await HydrateLabelOwnersAsync(package.LabelOwners.ToList());
    }

    private async Task<List<WorkItem>> GetWorkItemsAsync(List<int> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var idList = string.Join(",", ids);
        var query = $"SELECT [System.Id] FROM WorkItems WHERE [System.Id] IN ({idList})";
        return await _devOpsService.FetchWorkItemsPagedAsync(query, expand: WorkItemExpand.Relations);
    }

    private CodeownersViewResult BuildViewResult(List<PackageWorkItem> packages, List<LabelOwnerWorkItem> labelOwners)
    {
        var result = new CodeownersViewResult();

        if (packages.Count > 0)
        {
            result.Packages = packages.Select(p => new PackageViewItem
            {
                PackageName = p.PackageName,
                Language = p.Language,
                PackageType = p.PackageType,
                Owners = p.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                Labels = p.Labels.Select(l => l.LabelName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
            }).ToList();
        }

        var pathBased = labelOwners.Where(lo => !string.IsNullOrEmpty(lo.RepoPath)).ToList();
        var pathless = labelOwners.Where(lo => string.IsNullOrEmpty(lo.RepoPath)).ToList();

        if (pathBased.Count > 0)
        {
            result.PathBasedLabelOwners = pathBased
                .GroupBy(lo => lo.RepoPath)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LabelOwnerGroup
                {
                    GroupKey = g.Key,
                    Repository = g.First().Repository,
                    Items = g.Select(lo => new LabelOwnerViewItem
                    {
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                        Labels = lo.Labels.Select(l => l.LabelName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
                    }).ToList()
                }).ToList();
        }

        if (pathless.Count > 0)
        {
            result.PathlessLabelOwners = pathless
                .GroupBy(lo => string.Join(", ", lo.Labels.Select(l => l.LabelName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new LabelOwnerGroup
                {
                    GroupKey = g.Key,
                    Items = g.Select(lo => new LabelOwnerViewItem
                    {
                        LabelType = lo.LabelType,
                        Owners = lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
                        Labels = lo.Labels.Select(l => l.LabelName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
                    }).ToList()
                }).ToList();
        }

        return result;
    }

    #endregion
}
