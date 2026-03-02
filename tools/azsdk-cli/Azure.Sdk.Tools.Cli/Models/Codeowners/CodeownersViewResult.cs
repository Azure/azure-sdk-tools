// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Structured result for the CODEOWNERS view command.
/// Renders directly from the hydrated Azure DevOps model objects.
/// </summary>
public class CodeownersViewResponse : CommandResponse
{
    [JsonPropertyName("packages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PackageWorkItem>? Packages { get; set; }

    [JsonPropertyName("path_based_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerWorkItem>? PathBasedLabelOwners { get; set; }

    [JsonPropertyName("pathless_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerWorkItem>? PathlessLabelOwners { get; set; }

    public CodeownersViewResponse() { }

    public CodeownersViewResponse(List<PackageWorkItem> packages, List<LabelOwnerWorkItem> labelOwners)
    {
        if (packages.Count > 0)
        {
            Packages = packages;
        }

        // Split label owners into path-based and pathless
        var pathBased = labelOwners.Where(lo => !string.IsNullOrEmpty(lo.RepoPath)).ToList();
        var pathless = labelOwners.Where(lo => string.IsNullOrEmpty(lo.RepoPath)).ToList();

        if (pathBased.Count > 0)
        {
            PathBasedLabelOwners = pathBased
                .OrderBy(lo => lo.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lo => lo.RepoPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (pathless.Count > 0)
        {
            PathlessLabelOwners = pathless
                .OrderBy(lo => lo.Repository, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lo => string.Join("|", lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (Packages?.Count > 0)
        {
            sb.AppendLine("=== Packages ===");
            foreach (var pkg in Packages)
            {
                sb.AppendLine($"  Package: {pkg.PackageName} (Language: {pkg.Language}, Type: {pkg.PackageType}) [{pkg.WorkItemId}]");
                if (pkg.Owners.Count > 0)
                {
                    sb.AppendLine($"    Source Owners: {FormatOwnersList(pkg.Owners)}");
                }
                if (pkg.Labels.Count > 0)
                {
                    sb.AppendLine($"    Labels: {string.Join(", ", pkg.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase))}");
                }
            }
        }

        if (PathBasedLabelOwners?.Count > 0)
        {
            sb.AppendLine("=== Path-Based Label Owners ===");
            foreach (var repoGroup in PathBasedLabelOwners
                .GroupBy(lo => lo.Repository, StringComparer.OrdinalIgnoreCase)
                .OrderBy(rg => rg.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  Repo: {repoGroup.Key}");
                foreach (var pathGroup in repoGroup
                    .GroupBy(lo => lo.RepoPath, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(pg => pg.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"    Path: {pathGroup.Key}");
                    foreach (var lo in pathGroup)
                    {
                        sb.AppendLine($"      Type: {lo.LabelType} [{lo.WorkItemId}]");
                        if (lo.Owners.Count > 0)
                        {
                            sb.AppendLine($"        Owners: {FormatOwnersList(lo.Owners)}");
                        }
                        if (lo.Labels.Count > 0)
                        {
                            sb.AppendLine($"        Labels: {string.Join(", ", lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase))}");
                        }
                    }
                }
            }
        }

        if (PathlessLabelOwners?.Count > 0)
        {
            sb.AppendLine("=== Pathless Label Owners ===");
            foreach (var repoGroup in PathlessLabelOwners
                .GroupBy(lo => lo.Repository, StringComparer.OrdinalIgnoreCase)
                .OrderBy(rg => rg.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  Repo: {repoGroup.Key}");
                foreach (var lo in repoGroup)
                {
                    sb.AppendLine($"    Labels: {string.Join(", ", lo.Labels.Select(l => l.LabelName).OrderBy(l => l, StringComparer.OrdinalIgnoreCase))} [{lo.WorkItemId}]");
                    sb.AppendLine($"      Type: {lo.LabelType}");
                    if (lo.Owners.Count > 0)
                    {
                        sb.AppendLine($"      Owners: {FormatOwnersList(lo.Owners)}");
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a list of owners, showing expanded team members when available.
    /// Teams are displayed as "azure/team-name (member1, member2, member3)".
    /// </summary>
    private static string FormatOwnersList(List<OwnerWorkItem> owners)
    {
        return string.Join(", ", owners.Select(FormatOwnerAlias).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatOwnerAlias(OwnerWorkItem owner)
    {
        if (owner.ExpandedMembers.Count > 0)
        {
            var members = string.Join(", ", owner.ExpandedMembers.OrderBy(m => m, StringComparer.OrdinalIgnoreCase));
            return $"{owner.GitHubAlias} ({members})";
        }
        return owner.GitHubAlias;
    }
}
