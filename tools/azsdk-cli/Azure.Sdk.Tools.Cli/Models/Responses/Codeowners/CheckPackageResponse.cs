// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

public class CheckPackageResponse : CommandResponse
{
    [JsonPropertyName("directory_path")]
    public string DirectoryPath { get; set; } = string.Empty;

    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("owner_prompt_user")]
    public string OwnerPromptUser { get; set; } = CheckPackageHelper.CurrentGitHubUserPlaceholder;

    [JsonPropertyName("matched_path_expression")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedPathExpression { get; set; }

    [JsonPropertyName("resolved_target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvedTarget { get; set; }

    [JsonPropertyName("resolved_target_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvedTargetType { get; set; }

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = new();

    [JsonPropertyName("pr_labels")]
    public List<string> PRLabels { get; set; } = new();

    [JsonPropertyName("service_owners")]
    public List<string> ServiceOwners { get; set; } = new();

    [JsonPropertyName("service_labels")]
    public List<string> ServiceLabels { get; set; } = new();

    [JsonPropertyName("issues")]
    public List<CheckPackageIssue> Issues { get; } = [];

    [JsonIgnore]
    public int IssueCount => Issues.Count;

    [JsonPropertyName("response_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public override string? ResponseError
    {
        get => IssueCount switch
        {
            0 => base.ResponseError,
            1 => Issues[0].Message,
            _ => $"check-package found {IssueCount} issue(s) for path '{DirectoryPath}'."
        };
        set => base.ResponseError = value;
    }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Path: {DirectoryPath}");
        sb.AppendLine($"Package: {PackageName}");
        if (!string.IsNullOrEmpty(Repo))
        {
            sb.AppendLine($"Repo: {Repo}");
        }
        sb.AppendLine($"Owner Prompt User: {OwnerPromptUser}");
        if (!string.IsNullOrEmpty(MatchedPathExpression))
        {
            sb.AppendLine($"Matched Path Expression: {MatchedPathExpression}");
        }
        if (!string.IsNullOrEmpty(ResolvedTarget))
        {
            sb.AppendLine($"Resolved Target: {ResolvedTarget}");
        }
        if (!string.IsNullOrEmpty(ResolvedTargetType))
        {
            sb.AppendLine($"Resolved Target Type: {ResolvedTargetType}");
        }

        if (IssueCount == 0)
        {
            sb.AppendLine($"Owners: {string.Join(", ", Owners)}");
            sb.AppendLine($"PR Labels: {string.Join(", ", PRLabels)}");
            sb.AppendLine($"Service Labels: {string.Join(", ", ServiceLabels)}");
            sb.AppendLine($"Service Owners: {string.Join(", ", ServiceOwners)}");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"Issues: {IssueCount}");
        foreach (var issue in Issues)
        {
            sb.AppendLine($"- [{issue.Code}] {issue.Message}");
            sb.AppendLine($"  Next step: {issue.NextStep}");

            if (issue.CurrentValues?.Count > 0)
            {
                sb.AppendLine($"  Current values: {string.Join(", ", issue.CurrentValues)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public override string ToString()
    {
        var messages = new List<string>();
        var formatted = Format();
        var responseError = ResponseError;
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            messages.Add(formatted);
        }

        if (!string.IsNullOrEmpty(responseError))
        {
            messages.Add("[ERROR] " + responseError);
        }

        foreach (var error in ResponseErrors ?? [])
        {
            messages.Add("[ERROR] " + error);
        }

        if (SupportChannel != null)
        {
            messages.Add(SupportChannel);
        }

        return string.Join(Environment.NewLine, messages);
    }
}
