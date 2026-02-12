// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Container for all work item data needed to render CODEOWNERS entries.
/// </summary>
public record WorkItemData(
    Dictionary<int, PackageWorkItem> Packages,
    Dictionary<int, OwnerWorkItem> Owners,
    Dictionary<int, LabelWorkItem> Labels,
    List<LabelOwnerWorkItem> LabelOwners
)
{
    /// <summary>
    /// Populates the hydrated reference properties on PackageWorkItem and LabelOwnerWorkItem instances.
    /// Call this after all work items have been fetched.
    /// </summary>
    public void HydrateRelationships()
    {
        var labelOwnerLookup = LabelOwners.ToDictionary(lo => lo.WorkItemId);

        // Hydrate packages
        foreach (var pkg in Packages.Values)
        {
            foreach (var id in pkg.RelatedIds)
            {
                if (Owners.TryGetValue(id, out var owner))
                {
                    pkg.Owners.Add(owner);
                }
                else if (Labels.TryGetValue(id, out var label))
                {
                    pkg.Labels.Add(label);
                }
                else if (labelOwnerLookup.TryGetValue(id, out var labelOwner))
                {
                    pkg.LabelOwners.Add(labelOwner);
                }
            }
        }

        // Hydrate label owners
        foreach (var lo in LabelOwners)
        {
            foreach (var id in lo.RelatedIds)
            {
                if (Owners.TryGetValue(id, out var owner))
                {
                    lo.Owners.Add(owner);
                }
                else if (Labels.TryGetValue(id, out var label))
                {
                    lo.Labels.Add(label);
                }
            }
        }
    }

    /// <summary>
    /// Extracts related work item IDs from a work item's relations.
    /// </summary>
    /// <param name="wi">The work item to extract relations from.</param>
    /// <returns>A set of related work item IDs.</returns>
    public static HashSet<int> ExtractRelatedIds(WorkItem wi) =>
        wi.Relations?
            .Where(r => r.Rel == "System.LinkTypes.Related" && r.Url?.Contains("/workItems/") == true)
            .Select(r => int.TryParse(r.Url![(r.Url.LastIndexOf('/') + 1)..], out int id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet() ?? [];
}
