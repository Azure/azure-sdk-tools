// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

public class OwnerResponse
{
    public OwnerResponse() { }

    public OwnerResponse(OwnerWorkItem ownerWorkItem)
    {
        var members = ownerWorkItem.ExpandedMembers.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();

        GitHubAlias = ownerWorkItem.GitHubAlias;
        Members = members.Count > 0 ? members : null;
    }

    [JsonPropertyName("github_alias")]
    public string GitHubAlias { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Members { get; set; }
}

public class PackageResponse
{
    public PackageResponse() { }

    public PackageResponse(PackageWorkItem packageWorkItem)
    {
        var owners = packageWorkItem.Owners
            .Select(owner => new OwnerResponse(owner))
            .OrderBy(owner => owner.GitHubAlias, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var labels = packageWorkItem.Labels
            .Select(label => label.LabelName)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WorkItemId = packageWorkItem.WorkItemId;
        PackageName = packageWorkItem.PackageName;
        Language = packageWorkItem.Language;
        PackageType = packageWorkItem.PackageType;
        Owners = owners.Count > 0 ? owners : null;
        Labels = labels.Count > 0 ? labels : null;
    }

    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("package_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PackageType { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OwnerResponse>? Owners { get; set; }

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }
}

public class LabelOwnerResponse
{
    public LabelOwnerResponse() { }

    public LabelOwnerResponse(LabelOwnerWorkItem labelOwnerWorkItem)
    {
        var owners = labelOwnerWorkItem.Owners
            .Select(owner => new OwnerResponse(owner))
            .OrderBy(owner => owner.GitHubAlias, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var labels = labelOwnerWorkItem.Labels
            .Select(label => label.LabelName)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WorkItemId = labelOwnerWorkItem.WorkItemId;
        Repo = labelOwnerWorkItem.Repository;
        Path = labelOwnerWorkItem.RepoPath;
        Type = labelOwnerWorkItem.LabelType;
        Owners = owners.Count > 0 ? owners : null;
        Labels = labels.Count > 0 ? labels : null;
    }

    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OwnerResponse>? Owners { get; set; }

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }
}

/// <summary>
/// Structured result for the CODEOWNERS view command.
/// Renders directly from the hydrated Azure DevOps model objects.
/// </summary>
public class CodeownersViewResponse : CommandResponse
{
    [JsonPropertyName("packages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PackageResponse>? Packages { get; set; }

    [JsonPropertyName("path_based_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerResponse>? PathBasedLabelOwners { get; set; }

    [JsonPropertyName("pathless_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerResponse>? PathlessLabelOwners { get; set; }

    public CodeownersViewResponse() { }

    public CodeownersViewResponse(List<PackageWorkItem> packages, List<LabelOwnerWorkItem> labelOwners)
    {
        if (packages.Count > 0)
        {
            Packages = packages.Select(package => new PackageResponse(package))
                .OrderBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Split label owners into path-based and pathless
        var pathBased = labelOwners.Where(lo => !string.IsNullOrEmpty(lo.RepoPath)).ToList();
        var pathless = labelOwners.Where(lo => string.IsNullOrEmpty(lo.RepoPath)).ToList();

        if (pathBased.Count > 0)
        {
            PathBasedLabelOwners = pathBased.Select(labelOwner => new LabelOwnerResponse(labelOwner))
                .OrderBy(lo => lo.Repo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lo => lo.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (pathless.Count > 0)
        {
            PathlessLabelOwners = pathless.Select(labelOwner => new LabelOwnerResponse(labelOwner))
                .OrderBy(lo => lo.Repo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(lo => string.Join("|", lo.Labels ?? []), StringComparer.OrdinalIgnoreCase)
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
                if (pkg.Owners?.Count > 0)
                {
                    sb.AppendLine($"    Source Owners: {FormatOwnersList(pkg.Owners)}");
                }
                if (pkg.Labels?.Count > 0)
                {
                    sb.AppendLine($"    Labels: {string.Join(", ", pkg.Labels)}");
                }
            }
        }

        if (PathBasedLabelOwners?.Count > 0)
        {
            sb.AppendLine("=== Path-Based Label Owners ===");
            foreach (var repoGroup in PathBasedLabelOwners
                .GroupBy(lo => lo.Repo, StringComparer.OrdinalIgnoreCase)
                .OrderBy(rg => rg.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  Repo: {repoGroup.Key}");
                foreach (var pathGroup in repoGroup
                    .GroupBy(lo => lo.Path, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(pg => pg.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"    Path: {pathGroup.Key}");
                    foreach (var lo in pathGroup)
                    {
                        sb.AppendLine($"      Type: {lo.Type} [{lo.WorkItemId}]");
                        if (lo.Owners?.Count > 0)
                        {
                            sb.AppendLine($"        Owners: {FormatOwnersList(lo.Owners)}");
                        }
                        if (lo.Labels?.Count > 0)
                        {
                            sb.AppendLine($"        Labels: {string.Join(", ", lo.Labels)}");
                        }
                    }
                }
            }
        }

        if (PathlessLabelOwners?.Count > 0)
        {
            sb.AppendLine("=== Pathless Label Owners ===");
            foreach (var repoGroup in PathlessLabelOwners
                .GroupBy(lo => lo.Repo, StringComparer.OrdinalIgnoreCase)
                .OrderBy(rg => rg.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  Repo: {repoGroup.Key}");
                foreach (var lo in repoGroup)
                {
                    sb.AppendLine($"    Labels: {string.Join(", ", lo.Labels ?? [])} [{lo.WorkItemId}]");
                    sb.AppendLine($"      Type: {lo.Type}");
                    if (lo.Owners?.Count > 0)
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
    private static string FormatOwnersList(List<OwnerResponse> owners)
    {
        return string.Join(", ", owners.Select(FormatOwnerDisplay).OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatOwnerDisplay(OwnerResponse owner)
    {
        if (owner.Members?.Count > 0)
        {
            return $"{owner.GitHubAlias} ({string.Join(", ", owner.Members)})";
        }

        return owner.GitHubAlias;
    }

}
