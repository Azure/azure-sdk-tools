// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Codeowners;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Fluent builder for creating test WorkItemData instances with automatic ID assignment and hydration.
/// </summary>
/// <example>
/// <code>
/// var data = new WorkItemDataBuilder()
///     .AddOwner("alice", out var aliceId)
///     .AddLabel("Storage", out var storageId)
///     .AddPackage("Azure.Storage.Blobs", out var pkgId, relatedTo: [aliceId, storageId])
///     .Build();
/// </code>
/// </example>
public class WorkItemDataBuilder
{
    private int _nextId = 1;
    private readonly List<PackageData> _packages = [];
    private readonly List<OwnerData> _owners = [];
    private readonly List<LabelData> _labels = [];
    private readonly List<LabelOwnerData> _labelOwners = [];

    private record PackageData(int Id, string Name, string Version, HashSet<int> RelatedIds);
    private record OwnerData(int Id, string GitHubAlias);
    private record LabelData(int Id, string LabelName);
    private record LabelOwnerData(int Id, string LabelType, string Repository, string RepoPath, HashSet<int> RelatedIds);

    /// <summary>
    /// Adds an Owner work item.
    /// </summary>
    /// <param name="gitHubAlias">GitHub alias for the owner</param>
    /// <param name="id">Output parameter receiving the auto-generated ID</param>
    /// <returns>This builder for chaining</returns>
    public WorkItemDataBuilder AddOwner(string gitHubAlias, out int id)
    {
        id = _nextId++;
        _owners.Add(new OwnerData(id, gitHubAlias));
        return this;
    }

    /// <summary>
    /// Adds a Label work item.
    /// </summary>
    /// <param name="labelName">Name of the label</param>
    /// <param name="id">Output parameter receiving the auto-generated ID</param>
    /// <returns>This builder for chaining</returns>
    public WorkItemDataBuilder AddLabel(string labelName, out int id)
    {
        id = _nextId++;
        _labels.Add(new LabelData(id, labelName));
        return this;
    }

    /// <summary>
    /// Adds a Package work item with optional relationships.
    /// </summary>
    /// <param name="packageName">Name of the package</param>
    /// <param name="id">Output parameter receiving the auto-generated ID</param>
    /// <param name="version">Package version (major.minor)</param>
    /// <param name="relatedTo">IDs of related work items (owners, labels, label owners)</param>
    /// <returns>This builder for chaining</returns>
    public WorkItemDataBuilder AddPackage(string packageName, out int id, string version = "", params int[] relatedTo)
    {
        id = _nextId++;
        _packages.Add(new PackageData(id, packageName, version, [.. relatedTo]));
        return this;
    }

    /// <summary>
    /// Adds a Label Owner work item with optional relationships.
    /// </summary>
    /// <param name="labelType">Type: "Service Owner", "Azure SDK Owner", or "PR Label"</param>
    /// <param name="id">Output parameter receiving the auto-generated ID</param>
    /// <param name="repository">Repository name (e.g., "Azure/azure-sdk-for-net")</param>
    /// <param name="repoPath">Optional repo path for service-level entries</param>
    /// <param name="relatedTo">IDs of related work items (owners, labels)</param>
    /// <returns>This builder for chaining</returns>
    public WorkItemDataBuilder AddLabelOwner(string labelType, out int id, string repository = "Azure/azure-sdk-for-net", string repoPath = "", params int[] relatedTo)
    {
        id = _nextId++;
        _labelOwners.Add(new LabelOwnerData(id, labelType, repository, repoPath, [.. relatedTo]));
        return this;
    }

    /// <summary>
    /// Adds a Service Owner type Label Owner.
    /// </summary>
    public WorkItemDataBuilder AddServiceOwner(out int id, string repository = "Azure/azure-sdk-for-net", string repoPath = "", params int[] relatedTo)
        => AddLabelOwner("Service Owner", out id, repository, repoPath, relatedTo);

    /// <summary>
    /// Adds an Azure SDK Owner type Label Owner.
    /// </summary>
    public WorkItemDataBuilder AddAzureSdkOwner(out int id, string repository = "Azure/azure-sdk-for-net", string repoPath = "", params int[] relatedTo)
        => AddLabelOwner("Azure SDK Owner", out id, repository, repoPath, relatedTo);

    /// <summary>
    /// Adds a PR Label type Label Owner (used for service-level path entries).
    /// </summary>
    public WorkItemDataBuilder AddPRLabelOwner(out int id, string repoPath, string repository = "Azure/azure-sdk-for-net", params int[] relatedTo)
        => AddLabelOwner("PR Label", out id, repository, repoPath, relatedTo);

    /// <summary>
    /// Builds the WorkItemData with all relationships hydrated.
    /// </summary>
    /// <returns>A fully hydrated WorkItemData instance</returns>
    public WorkItemData Build()
    {
        var packages = _packages.ToDictionary(
            p => p.Id,
            p => MapToPackageWorkItem(CreatePackageWorkItem(p.Id, p.Name, p.Version, p.RelatedIds)));

        var owners = _owners.ToDictionary(
            o => o.Id,
            o => MapToOwnerWorkItem(CreateOwnerWorkItem(o.Id, o.GitHubAlias)));

        var labels = _labels.ToDictionary(
            l => l.Id,
            l => MapToLabelWorkItem(CreateLabelWorkItem(l.Id, l.LabelName)));

        var labelOwners = _labelOwners
            .Select(lo => MapToLabelOwnerWorkItem(CreateLabelOwnerWorkItem(lo.Id, lo.LabelType, lo.Repository, lo.RepoPath, lo.RelatedIds)))
            .ToList();

        var data = new WorkItemData(packages, owners, labels, labelOwners);
        data.HydrateRelationships();
        return data;
    }

    #region WorkItem Factory Methods

    private static WorkItem CreatePackageWorkItem(int id, string packageName, string version, HashSet<int> relatedIds)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Package", packageName },
                { "Custom.PackageVersionMajorMinor", version }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
    }

    private static WorkItem CreateOwnerWorkItem(int id, string gitHubAlias)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.GitHubAlias", gitHubAlias }
            }
        };
    }

    private static WorkItem CreateLabelWorkItem(int id, string labelName)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.Label", labelName }
            }
        };
    }

    private static WorkItem CreateLabelOwnerWorkItem(int id, string labelType, string repository, string repoPath, HashSet<int> relatedIds)
    {
        return new WorkItem
        {
            Id = id,
            Fields = new Dictionary<string, object>
            {
                { "Custom.LabelType", labelType },
                { "Custom.Repository", repository },
                { "Custom.RepoPath", repoPath }
            },
            Relations = relatedIds.Select(relId => new WorkItemRelation
            {
                Rel = "System.LinkTypes.Related",
                Url = $"https://dev.azure.com/org/project/_apis/wit/workItems/{relId}"
            }).ToList()
        };
    }

    #endregion

    #region Mapper Methods

    private static PackageWorkItem MapToPackageWorkItem(WorkItem wi)
    {
        return new PackageWorkItem
        {
            WorkItemId = wi.Id!.Value,
            PackageName = GetFieldValue(wi, "Custom.Package"),
            PackageVersionMajorMinor = GetFieldValue(wi, "Custom.PackageVersionMajorMinor"),
            RelatedIds = wi.ExtractRelatedIds()
        };
    }

    private static OwnerWorkItem MapToOwnerWorkItem(WorkItem wi)
    {
        return new OwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            GitHubAlias = GetFieldValue(wi, "Custom.GitHubAlias")
        };
    }

    private static LabelWorkItem MapToLabelWorkItem(WorkItem wi)
    {
        return new LabelWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelName = GetFieldValue(wi, "Custom.Label")
        };
    }

    private static LabelOwnerWorkItem MapToLabelOwnerWorkItem(WorkItem wi)
    {
        return new LabelOwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelType = GetFieldValue(wi, "Custom.LabelType"),
            Repository = GetFieldValue(wi, "Custom.Repository"),
            RepoPath = GetFieldValue(wi, "Custom.RepoPath"),
            RelatedIds = wi.ExtractRelatedIds()
        };
    }

    private static string GetFieldValue(WorkItem wi, string fieldName)
    {
        return wi.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() ?? "" : "";
    }

    #endregion
}
