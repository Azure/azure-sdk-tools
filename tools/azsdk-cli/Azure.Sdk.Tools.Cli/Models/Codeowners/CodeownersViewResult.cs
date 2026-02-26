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
public class CodeownersViewResult : CommandResponse
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

    public CodeownersViewResult() { }

    public CodeownersViewResult(List<PackageWorkItem> packages, List<LabelOwnerWorkItem> labelOwners)
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
                    sb.AppendLine($"    Source Owners: {string.Join(", ", pkg.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase))}");
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
                            sb.AppendLine($"        Owners: {string.Join(", ", lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase))}");
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
                        sb.AppendLine($"      Owners: {string.Join(", ", lo.Owners.Select(o => o.GitHubAlias).OrderBy(a => a, StringComparer.OrdinalIgnoreCase))}");
                    }
                }
            }
        }

        return sb.ToString();
    }
}
