// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ReleasePlanDetails
    {
        public int WorkItemId { get; set; }
        public string WorkItemUrl { get; set; } = string.Empty;
        public string WorkItemHtmlUrl { get; set; } = string.Empty;
        public string ServiceTreeId { get; set; } = string.Empty;
        public string ProductTreeId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> SpecPullRequests { get; set; } = [];
        public string SDKReleaseMonth { get; set; } = string.Empty;
        public bool IsManagementPlane { get; set; } = false;
        public bool IsDataPlane { get; set; } = false;
        public string SpecAPIVersion { get; set; } = string.Empty;
        public string SpecType {  get; set; } = string.Empty;
        public string ReleasePlanLink { get; set; } = string.Empty;
        public bool IsTestReleasePlan { get; set; } = false;
        public int ReleasePlanId { get; set; }
        public string SDKReleaseType { get; set; } = string.Empty;
        public List<SDKInfo> SDKInfo { get; set; } = [];
        public string ReleasePlanSubmittedByEmail { get; set; } = string.Empty;
        public bool IsCreatedByAgent { get; set; }
        public string ActiveSpecPullRequest { get; set; } = string.Empty;
        public string SDKLanguages { get; set; } = string.Empty;
        public bool IsSpecApproved { get; set; } = false;
        public int ApiSpecWorkItemId { get; set; } = 0;
        public string LanguageExclusionRequesterNote { get; set; } = string.Empty;
        public string LanguageExclusionApproverNote { get; set; } = string.Empty;

        public WorkItemFields GetWorkItemFields()
        {
            return new WorkItemFields
            {
                ServiceTreeId = this.ServiceTreeId,
                ProductTreeId = this.ProductTreeId,
                SDKReleaseMonth = this.SDKReleaseMonth,
                IsManagementPlane = this.IsManagementPlane,
                IsDataPlane = this.IsDataPlane,
                SpecAPIVersion = this.SpecAPIVersion,
                SpecType = this.SpecType,
                IsTestReleasePlan = this.IsTestReleasePlan,
                SDKReleaseType = this.SDKReleaseType,
                ReleasePlanSubmittedByEmail = this.ReleasePlanSubmittedByEmail,
                IsCreatedByAgent = this.IsCreatedByAgent,
                SpecPullRequests = this.SpecPullRequests,
                ActiveSpecPullRequest = this.ActiveSpecPullRequest
            };
        }
    }

    public class SDKInfo
    {
        public string Language { get; set; } = string.Empty;
        public string GenerationPipelineUrl { get; set; } = string.Empty;
        public string SdkPullRequestUrl { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string GenerationStatus { get; set; } = string.Empty;
        public string ReleaseStatus { get; set; } = string.Empty;
        public string PullRequestStatus { get; set; } = string.Empty;
        public string ReleaseExclusionStatus { get; set; } = string.Empty;
    }
}
