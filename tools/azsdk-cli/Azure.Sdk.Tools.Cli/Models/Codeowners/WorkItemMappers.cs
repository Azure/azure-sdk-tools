using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

public static class WorkItemMappers
{
    /// <summary>
    /// Extracts related work item IDs from a work item's relations.
    /// </summary>
    /// <param name="wi">The work item to extract relations from.</param>
    /// <returns>A set of related work item IDs.</returns>
    public static HashSet<int> ExtractRelatedIds(this WorkItem wi) =>
        wi.Relations?
            .Where(r => r.Rel == "System.LinkTypes.Related" && r.Url?.Contains("/workItems/") == true)
            .Select(r => int.TryParse(r.Url![(r.Url.LastIndexOf('/') + 1)..], out int id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet() ?? [];

    public static PackageWorkItem MapToPackageWorkItem(WorkItem wi)
    {
        return new PackageWorkItem
        {
            WorkItemId = wi.Id!.Value,
            PackageName = wi.GetFieldValue("Custom.Package"),
            PackageVersionMajorMinor = wi.GetFieldValue("Custom.PackageVersionMajorMinor"),
            Language = wi.GetFieldValue("Custom.Language"),
            PackageType = wi.GetFieldValue("Custom.PackageType"),
            ServiceName = wi.GetFieldValue("Custom.ServiceName"),
            PackageDisplayName = wi.GetFieldValue("Custom.PackageDisplayName"),
            GroupId = wi.GetFieldValue("Custom.GroupId"),
            PackageRepoPath = wi.GetFieldValue("Custom.PackageRepoPath"),
            RelatedIds = wi.ExtractRelatedIds()
        };
    }
    public static OwnerWorkItem MapToOwnerWorkItem(WorkItem wi)
    {
        return new OwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            GitHubAlias = GetFieldValue(wi, "Custom.GitHubAlias")
        };
    }

    public static LabelWorkItem MapToLabelWorkItem(WorkItem wi)
    {
        return new LabelWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelName = GetFieldValue(wi, "Custom.Label")
        };
    }

    public static LabelOwnerWorkItem MapToLabelOwnerWorkItem(WorkItem wi)
    {
        return new LabelOwnerWorkItem
        {
            WorkItemId = wi.Id!.Value,
            LabelType = wi.GetFieldValue("Custom.LabelType"),
            Repository = wi.GetFieldValue("Custom.Repository"),
            RepoPath = wi.GetFieldValue("Custom.RepoPath"),
            RelatedIds = ExtractRelatedIds(wi)
        };
    }

    public static string GetFieldValue(this WorkItem wi, string fieldName)
    {
        return wi.Fields.TryGetValue(fieldName, out var value) ? value?.ToString() ?? "" : "";
    }

    /// <summary>
    /// Given a list of packages, return a list of unique packages at the latest
    /// version of that package in the original list
    /// </summary>
    /// <param name="packages">List of all packages</param>
    /// <returns>List of packages with only the latest version for each package name</returns>
    public static List<PackageWorkItem> GetLatestPackageVersions(List<PackageWorkItem> packages)
    {
        return packages
            .GroupBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(
                // Convert major.minor to major.minor.0 for proper comparison
                p => $"{p.PackageVersionMajorMinor}.0",
                VersionHelper.Default).First()
            )
            .ToList();
    }
}
