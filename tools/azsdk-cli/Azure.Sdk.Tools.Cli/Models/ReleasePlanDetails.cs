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

        public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument GetPatchDocument()
        {

            var jsonDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
            {
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ServiceTreeID",
                    Value = ServiceTreeId
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ProductServiceTreeID",
                    Value = ProductTreeId
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.SDKReleaseMonth",
                    Value = SDKReleaseMonth
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.MgmtScope",
                    Value = IsManagementPlane ? "Yes" : "No"
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.DataScope",
                    Value = IsDataPlane ? "Yes" : "No"
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.APISpecDefinitionType",
                    Value = SpecType
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.APISpecversion",
                    Value = SpecAPIVersion
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.SDKtypetobereleased",
                    Value = SDKReleaseType
                }
            };

            // Add release plan test tag if this is a test release plan
            if (IsTestReleasePlan)
            {
                var tag = new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Tags",
                    Value = "Release Planner App Test"
                };
                jsonDocument.Add(tag);
            }

            // Add flag in release plan to indicate that it's used by Copilot agent
            if (IsCreatedByAgent)
            {
                var createdUsingAgent = new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.CreatedUsing",
                    Value = "Copilot"
                };
                jsonDocument.Add(createdUsingAgent);
            }

            // Add release plan submitted by email field
            if (!string.IsNullOrEmpty(ReleasePlanSubmittedByEmail))
            {
                var submittedByEmail = new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ReleasePlanSubmittedby",
                    Value = ReleasePlanSubmittedByEmail
                };
                jsonDocument.Add(submittedByEmail);
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
