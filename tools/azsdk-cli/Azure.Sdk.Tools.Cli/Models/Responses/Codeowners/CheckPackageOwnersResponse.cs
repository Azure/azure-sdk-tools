// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured response for the check-package-owners command.
/// Reuses PackageResponse and LabelOwnerResponse from the view command.
/// </summary>
public class CheckPackageOwnersResponse : CommandResponse
{
    [JsonPropertyName("package")]
    public string Package { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("pass")]
    public bool Pass { get; set; }

    [JsonPropertyName("package_work_item")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PackageResponse? PackageWorkItem { get; set; }

    [JsonPropertyName("path_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LabelOwnerResponse? PathOwners { get; set; }

    [JsonPropertyName("pr_labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PrLabels { get; set; }

    [JsonPropertyName("service_owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LabelOwnerResponse? ServiceOwners { get; set; }

    public override int ExitCode
    {
        get => Pass ? 0 : 1;
        set { }
    }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Check Package Owners: {Package} ===");
        sb.AppendLine($"  Path: {Path}");
        sb.AppendLine();

        if (PackageWorkItem != null)
        {
            sb.AppendLine($"  Package: {PackageWorkItem.PackageName} [{PackageWorkItem.WorkItemId}]");
            if (PackageWorkItem.Owners?.Count > 0)
            {
                sb.AppendLine($"    Owners: {FormatOwnersList(PackageWorkItem.Owners)}");
            }
            else
            {
                sb.AppendLine("    Owners: (none)");
            }
            if (PackageWorkItem.Labels?.Count > 0)
            {
                sb.AppendLine($"    Labels: {string.Join(", ", PackageWorkItem.Labels)}");
            }
        }

        if (PathOwners != null)
        {
            sb.AppendLine();
            sb.AppendLine($"  Path Owners: [{PathOwners.WorkItemId}]");
            if (PathOwners.Path != null)
            {
                sb.AppendLine($"    Matched Path: {PathOwners.Path}");
            }
            if (PathOwners.Owners?.Count > 0)
            {
                sb.AppendLine($"    Owners: {FormatOwnersList(PathOwners.Owners)}");
            }
            if (PathOwners.Labels?.Count > 0)
            {
                sb.AppendLine($"    Labels: {string.Join(", ", PathOwners.Labels)}");
            }
        }

        if (PrLabels?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"  PR Labels: {string.Join(", ", PrLabels)}");
        }

        if (ServiceOwners != null)
        {
            sb.AppendLine();
            sb.AppendLine($"  Service Owners: [{ServiceOwners.WorkItemId}]");
            if (ServiceOwners.Owners?.Count > 0)
            {
                sb.AppendLine($"    Owners: {FormatOwnersList(ServiceOwners.Owners)}");
            }
            else
            {
                sb.AppendLine("    Owners: (none)");
            }
            if (ServiceOwners.Labels?.Count > 0)
            {
                sb.AppendLine($"    Labels: {string.Join(", ", ServiceOwners.Labels)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(Pass ? "  Result: PASS" : "  Result: FAIL");
        return sb.ToString();
    }

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
