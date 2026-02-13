// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Response model for the CODEOWNERS view command.
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

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }

        if (Packages?.Count > 0)
        {
            sb.AppendLine("Packages:");
            foreach (var pkg in Packages)
            {
                sb.AppendLine($"  - {pkg.PackageName} ({pkg.Language}, {pkg.PackageType})");
                if (pkg.Owners?.Count > 0)
                {
                    sb.AppendLine($"    Owners: {string.Join(", ", pkg.Owners)}");
                }
                if (pkg.Labels?.Count > 0)
                {
                    sb.AppendLine($"    Labels: {string.Join(", ", pkg.Labels)}");
                }
            }
        }

        if (PathBasedLabelOwners?.Count > 0)
        {
            sb.AppendLine("Path-based Label Owners:");
            foreach (var group in PathBasedLabelOwners)
            {
                sb.AppendLine($"  Path: {group.GroupKey} (Repo: {group.Repository})");
                foreach (var item in group.Items ?? [])
                {
                    sb.AppendLine($"    [{item.LabelType}]");
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
            sb.AppendLine("Pathless Label Owners:");
            foreach (var group in PathlessLabelOwners)
            {
                sb.AppendLine($"  Labels: {group.GroupKey}");
                foreach (var item in group.Items ?? [])
                {
                    sb.AppendLine($"    [{item.LabelType}]");
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

public class PackageViewItem
{
    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("package_type")]
    public string PackageType { get; set; } = string.Empty;

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = [];

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];
}

public class LabelOwnerGroup
{
    [JsonPropertyName("group_key")]
    public string GroupKey { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repository { get; set; }

    [JsonPropertyName("items")]
    public List<LabelOwnerViewItem> Items { get; set; } = [];
}

public class LabelOwnerViewItem
{
    [JsonPropertyName("label_type")]
    public string LabelType { get; set; } = string.Empty;

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = [];

    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = [];
}
