// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Result of an individual ownership or label check.
/// </summary>
public class OwnershipCheck
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Required { get; set; }

    [JsonPropertyName("actual")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Actual { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Owners { get; set; }
}

/// <summary>
/// Result of a PR label existence check.
/// </summary>
public class PrLabelCheck
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }
}

/// <summary>
/// Result of a service owner check including label superset matching.
/// </summary>
public class ServiceOwnerCheck
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Required { get; set; }

    [JsonPropertyName("actual")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Actual { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Owners { get; set; }

    [JsonPropertyName("required_labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RequiredLabels { get; set; }

    [JsonPropertyName("matched_labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? MatchedLabels { get; set; }
}

/// <summary>
/// Result of the path-based fallback PR Label owner check.
/// </summary>
public class PrLabelOwnerCheck
{
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Required { get; set; }

    [JsonPropertyName("actual")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Actual { get; set; }

    [JsonPropertyName("owners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Owners { get; set; }

    [JsonPropertyName("labels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Labels { get; set; }

    [JsonPropertyName("matched_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedPath { get; set; }
}

/// <summary>
/// Combined result of the path-based fallback checks.
/// </summary>
public class PathFallbackCheckResult
{
    [JsonPropertyName("pr_label_owner_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrLabelOwnerCheck? PrLabelOwnerCheck { get; set; }

    [JsonPropertyName("service_owner_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ServiceOwnerCheck? ServiceOwnerCheck { get; set; }
}

/// <summary>
/// Structured response for the check-package-owners command.
/// </summary>
public class CheckPackageOwnersResponse : CommandResponse
{
    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("directory_path")]
    public string DirectoryPath { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    [JsonPropertyName("validation_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValidationPath { get; set; }

    [JsonPropertyName("owner_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OwnershipCheck? OwnerCheck { get; set; }

    [JsonPropertyName("pr_label_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrLabelCheck? PrLabelCheck { get; set; }

    [JsonPropertyName("service_owner_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ServiceOwnerCheck? ServiceOwnerCheck { get; set; }

    [JsonPropertyName("path_fallback_check")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PathFallbackCheckResult? PathFallbackCheck { get; set; }

    [JsonPropertyName("all_passed")]
    public bool AllPassed { get; set; }

    public override int ExitCode
    {
        get => AllPassed ? 0 : 1;
        set { }
    }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Check Package Owners: {PackageName} ===");
        sb.AppendLine($"  Repo: {Repo}");
        sb.AppendLine($"  Directory Path: {DirectoryPath}");

        if (ValidationPath != null)
        {
            sb.AppendLine($"  Validation Path: {ValidationPath}");
        }

        if (ValidationPath == "Package")
        {
            FormatPrimaryPath(sb);
        }
        else if (ValidationPath == "PathFallback")
        {
            FormatFallbackPath(sb);
        }

        sb.AppendLine();
        sb.AppendLine(AllPassed ? "  Result: PASS" : "  Result: FAIL");
        return sb.ToString();
    }

    private void FormatPrimaryPath(StringBuilder sb)
    {
        if (OwnerCheck != null)
        {
            var status = OwnerCheck.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"  Package Owners: [{status}] {OwnerCheck.Actual}/{OwnerCheck.Required} unique individuals");
            if (OwnerCheck.Owners?.Count > 0)
            {
                sb.AppendLine($"    Owners: {string.Join(", ", OwnerCheck.Owners)}");
            }
        }

        if (PrLabelCheck != null)
        {
            var status = PrLabelCheck.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"  PR Labels: [{status}]");
            if (PrLabelCheck.Labels?.Count > 0)
            {
                sb.AppendLine($"    Labels: {string.Join(", ", PrLabelCheck.Labels)}");
            }
        }

        if (ServiceOwnerCheck != null)
        {
            FormatServiceOwnerCheck(sb, ServiceOwnerCheck, "  ");
        }
    }

    private void FormatFallbackPath(StringBuilder sb)
    {
        if (PathFallbackCheck == null)
        {
            return;
        }

        if (PathFallbackCheck.PrLabelOwnerCheck != null)
        {
            var check = PathFallbackCheck.PrLabelOwnerCheck;
            var status = check.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"  PR Label Owners (path-based): [{status}] {check.Actual}/{check.Required} unique individuals");
            if (check.MatchedPath != null)
            {
                sb.AppendLine($"    Matched Path: {check.MatchedPath}");
            }
            if (check.Owners?.Count > 0)
            {
                sb.AppendLine($"    Owners: {string.Join(", ", check.Owners)}");
            }
            if (check.Labels?.Count > 0)
            {
                sb.AppendLine($"    Labels: {string.Join(", ", check.Labels)}");
            }
        }

        if (PathFallbackCheck.ServiceOwnerCheck != null)
        {
            FormatServiceOwnerCheck(sb, PathFallbackCheck.ServiceOwnerCheck, "  ");
        }
    }

    private static void FormatServiceOwnerCheck(StringBuilder sb, ServiceOwnerCheck check, string indent)
    {
        var status = check.Passed ? "PASS" : "FAIL";
        sb.AppendLine($"{indent}Service Owners: [{status}] {check.Actual}/{check.Required} unique individuals");
        if (check.RequiredLabels?.Count > 0)
        {
            sb.AppendLine($"{indent}  Required Labels: {string.Join(", ", check.RequiredLabels)}");
        }
        if (check.MatchedLabels?.Count > 0)
        {
            sb.AppendLine($"{indent}  Matched Labels: {string.Join(", ", check.MatchedLabels)}");
        }
        if (check.Owners?.Count > 0)
        {
            sb.AppendLine($"{indent}  Owners: {string.Join(", ", check.Owners)}");
        }
    }
}
