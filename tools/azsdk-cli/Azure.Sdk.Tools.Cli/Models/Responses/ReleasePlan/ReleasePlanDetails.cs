// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

/// <summary>
/// Public release-plan information. Deliberately does not inherit from an ADO
/// work-item model: new storage fields must not silently become tool output.
/// Existing public JSON field names are retained for compatibility.
/// </summary>
public sealed class ReleasePlanDetails(ReleasePlanWorkItem plan)
{
    [JsonPropertyName("ReleasePlanId")]
    public int ReleasePlanId { get; } = plan.DisplayId;

    [JsonPropertyName("ReleasePlanLink")]
    public string ReleasePlanLink { get; } = plan.ReleasePlanLink;

    [JsonPropertyName("Title")]
    public string Title { get; } = plan.Title;

    [JsonPropertyName("Status")]
    public string Status { get; } = plan.Status;

    [JsonPropertyName("Owner")]
    public string Owner { get; } = plan.Owner;

    [JsonPropertyName("CreatedDate")]
    public DateTime CreatedDate { get; } = plan.CreatedDate;

    [JsonPropertyName("ChangedDate")]
    public DateTime ChangedDate { get; } = plan.ChangedDate;

    [JsonPropertyName("ServiceTreeId")]
    public string ServiceTreeId { get; } = plan.ServiceTreeId;

    [JsonPropertyName("ProductTreeId")]
    public string ProductTreeId { get; } = plan.ProductTreeId;

    [JsonPropertyName("ProductName")]
    public string ProductName { get; } = plan.ProductName;

    [JsonPropertyName("ProductType")]
    public string ProductType { get; } = plan.ProductType;

    [JsonPropertyName("ProductLifecycle")]
    public string ProductLifecycle { get; } = plan.ProductLifecycle;

    [JsonPropertyName("SDKReleaseMonth")]
    public string SDKReleaseMonth { get; } = plan.SDKReleaseMonth;

    [JsonPropertyName("SDKReleaseType")]
    public string SDKReleaseType { get; } = plan.SDKReleaseType;

    [JsonPropertyName("ReleasePlanType")]
    public string ReleasePlanType { get; } = plan.ReleasePlanType;

    [JsonPropertyName("ApiReleaseType")]
    public ApiReleaseType ApiReleaseType { get; } = plan.ApiReleaseType;

    [JsonPropertyName("IsManagementPlane")]
    public bool IsManagementPlane { get; } = plan.IsManagementPlane;

    [JsonPropertyName("IsDataPlane")]
    public bool IsDataPlane { get; } = plan.IsDataPlane;

    [JsonPropertyName("IsTestReleasePlan")]
    public bool IsTestReleasePlan { get; } = plan.IsTestReleasePlan;

    [JsonPropertyName("SpecType")]
    public string SpecType { get; } = plan.SpecType;

    [JsonPropertyName("SpecAPIVersion")]
    public string SpecAPIVersion { get; } = plan.SpecAPIVersion;

    [JsonPropertyName("APISpecProjectPath")]
    public string APISpecProjectPath { get; } = plan.APISpecProjectPath;

    [JsonPropertyName("ActiveSpecPullRequest")]
    public string ActiveSpecPullRequest { get; } = plan.ActiveSpecPullRequest;

    [JsonPropertyName("SpecPullRequests")]
    public IReadOnlyList<string> SpecPullRequests { get; } = plan.SpecPullRequests;

    [JsonPropertyName("IsSpecApproved")]
    public bool IsSpecApproved { get; } = plan.IsSpecApproved;

    [JsonPropertyName("SDKLanguages")]
    public string SDKLanguages { get; } = plan.SDKLanguages;

    [JsonPropertyName("SDKInfo")]
    public IReadOnlyList<ReleasePlanSdkDetails> SDKInfo { get; } = plan.SDKInfo.Select(sdk => new ReleasePlanSdkDetails(sdk)).ToArray();

    [JsonPropertyName("AttestationStatus")]
    public string AttestationStatus { get; } = plan.AttestationStatus;

    [JsonPropertyName("LanguageExclusionRequesterNote")]
    public string LanguageExclusionRequesterNote { get; } = plan.LanguageExclusionRequesterNote;

    [JsonPropertyName("LanguageExclusionApproverNote")]
    public string LanguageExclusionApproverNote { get; } = plan.LanguageExclusionApproverNote;
}