// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ReleasePlanDetails : WorkItemBase
    {
        [FieldName("Custom.ServiceTreeID")]
        public string ServiceTreeId { get; set; } = string.Empty;

        [FieldName("Custom.ProductServiceTreeID")]
        public string ProductTreeId { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        [FieldName("Custom.RESTAPIReviews")]
        public List<string> SpecPullRequests { get; set; } = [];

        [FieldName("Custom.SDKReleaseMonth")]
        public string SDKReleaseMonth { get; set; } = string.Empty;

        [FieldName("Custom.MgmtScope")]
        public bool IsManagementPlane { get; set; } = false;

        [FieldName("Custom.DataScope")]
        public bool IsDataPlane { get; set; } = false;

        [FieldName("Custom.APISpecversion")]
        public string SpecAPIVersion { get; set; } = string.Empty;

        [FieldName("Custom.APISpecDefinitionType")]
        public string SpecType {  get; set; } = string.Empty;

        public string ReleasePlanLink { get; set; } = string.Empty;

        public bool IsTestReleasePlan { get; set; } = false;

        public int ReleasePlanId { get; set; }

        [FieldName("Custom.SDKtypetobereleased")]
        public string SDKReleaseType { get; set; } = string.Empty;

        public List<SDKInfo> SDKInfo { get; set; } = [];

        [FieldName("Custom.ReleasePlanSubmittedby")]
        public string ReleasePlanSubmittedByEmail { get; set; } = string.Empty;

        [FieldName("Custom.ActiveSpecPullRequestUrl")]
        public string ActiveSpecPullRequest { get; set; } = string.Empty;

        public string SDKLanguages { get; set; } = string.Empty;

        public bool IsSpecApproved { get; set; } = false;

        public int ApiSpecWorkItemId { get; set; } = 0;

        public string LanguageExclusionRequesterNote { get; set; } = string.Empty;

        public string LanguageExclusionApproverNote { get; set; } = string.Empty;

        public override Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument GetPatchDocument(string? workItemType = null)
        {
            var jsonDocument = base.GetPatchDocument(workItemType);

            if (IsTestReleasePlan)
            {
                var releasePlanTag = "Release Planner App Test";
                var tagValue = string.IsNullOrEmpty(Tag) ? releasePlanTag : $"{Tag},{releasePlanTag}";
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Tags",
                    Value = tagValue
                });
            }

            return jsonDocument;
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
