// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Structured result for the CODEOWNERS view command.
/// </summary>
public class CodeownersViewResult : CommandResponse
{
    [JsonPropertyName("packages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PackageViewItem>? Packages { get; set; }

    [JsonPropertyName("path_based_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerGroup>? PathBasedLabelOwners { get; set; }

    [JsonPropertyName("pathless_label_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LabelOwnerGroup>? PathlessLabelOwners { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (Packages?.Count > 0)
        {
            sb.AppendLine("=== Packages ===");
            foreach (var pkg in Packages)
            {
                sb.AppendLine($"  Package: {pkg.PackageName} (Language: {pkg.Language}, Type: {pkg.PackageType})");
                if (pkg.SourceOwners?.Count > 0)
                {
                    sb.AppendLine($"    Source Owners: {string.Join(", ", pkg.SourceOwners)}");
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
            foreach (var group in PathBasedLabelOwners)
            {
                sb.AppendLine($"  Path: {group.Path} (Repo: {group.Repo})");
                foreach (var item in group.Items ?? [])
                {
                    sb.AppendLine($"    Type: {item.LabelType}");
                    if (item.Owners?.Count > 0)
                    {
                        sb.AppendLine($"      Owners: {string.Join(", ", item.Owners)}");
                    }
                    if (item.Labels?.Count > 0)
                    {
                        sb.AppendLine($"      Labels: {string.Join(", ", item.Labels)}");
                    }
                }
            }
        }

        if (PathlessLabelOwners?.Count > 0)
        {
            sb.AppendLine("=== Pathless Label Owners ===");
            foreach (var group in PathlessLabelOwners)
            {
                sb.AppendLine($"  Labels: {string.Join(", ", group.LabelSet ?? [])}");
                foreach (var item in group.Items ?? [])
                {
                    sb.AppendLine($"    Type: {item.LabelType}");
                    if (item.Owners?.Count > 0)
                    {
                        sb.AppendLine($"      Owners: {string.Join(", ", item.Owners)}");
                    }
                }
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// A package with its associated owners and labels.
/// </summary>
public class PackageViewItem
{
    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("package_type")]
    public string PackageType { get; set; } = string.Empty;

    [JsonPropertyName("source_owners")]
    public List<string> SourceOwners { get; set; } = [];

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];
}

/// <summary>
/// A group of label owners, either path-based or pathless.
/// </summary>
public class LabelOwnerGroup
{
    /// <summary>
    /// For path-based groups, the repo path.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; set; }

    /// <summary>
    /// Repository name.
    /// </summary>
    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    /// <summary>
    /// For pathless groups, the alphabetized label set.
    /// </summary>
    [JsonPropertyName("label_set")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LabelSet { get; set; }

    [JsonPropertyName("items")]
    public List<LabelOwnerViewItem> Items { get; set; } = [];
}

/// <summary>
/// A single label owner entry within a group.
/// </summary>
public class LabelOwnerViewItem
{
    [JsonPropertyName("label_type")]
    public string LabelType { get; set; } = string.Empty;

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = [];

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }
}
