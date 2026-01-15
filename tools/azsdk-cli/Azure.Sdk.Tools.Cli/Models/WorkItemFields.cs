// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models.Process;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class WorkItemFields
    {
        public string Language { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string PackageDisplayName { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty;
        public bool PackageTypeNewLibrary { get; set; } = false;
        public string PackageVersionMajorMinor { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string PackageRepoPath { get; set; } = string.Empty;
        public string EpicType { get; set; } = string.Empty;
        public string ServiceTreeId { get; set; } = string.Empty;
        public string ProductTreeId { get; set; } = string.Empty;
        public string SDKReleaseMonth { get; set; } = string.Empty;
        public bool IsManagementPlane { get; set; } = false;
        public bool IsDataPlane { get; set; } = false;
        public string SpecAPIVersion { get; set; } = string.Empty;
        public string SpecType { get; set; } = string.Empty;
        public bool IsTestReleasePlan { get; set; } = false;
        public string SDKReleaseType { get; set; } = string.Empty;
        public string ReleasePlanSubmittedByEmail { get; set; } = string.Empty;
        public bool IsCreatedByAgent { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public List<string> SpecPullRequests { get; set; } = [];
        public string ActiveSpecPullRequest { get; set; } = string.Empty;

        public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument GetPatchDocument(string workItemType = null, string? title = null)
        {

            var jsonDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
            {
                // Package Fields
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.Language",
                    Value = Language
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.Package",
                    Value = PackageName
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.GroupId",
                    Value = GroupId
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.PackageDisplayName",
                    Value = PackageDisplayName
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.PackageType",
                    Value = PackageType
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.PackageTypeNewLibrary",
                    Value = PackageTypeNewLibrary ? "Yes" : "No"
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.PackageVersionMajorMinor",
                    Value = PackageVersionMajorMinor
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ServiceName",
                    Value = ServiceName
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.PackageRepoPath",
                    Value = PackageRepoPath
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.EpicType",
                    Value = EpicType
                },
                new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.AssignedTo",
                    Value = AssignedTo
                },
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
                var releasePlanTag = "Release Planner App Test";
                var value = string.IsNullOrEmpty(Tag) ? releasePlanTag : $"{Tag},{releasePlanTag}";
                var tag = new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Tags",
                    Value = value
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

            if (!string.IsNullOrEmpty(workItemType) && workItemType == "API Spec" && SpecPullRequests.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var pr in SpecPullRequests)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append("<br>");
                    }
                    sb.Append($"<a href=\"{pr}\">{pr}</a>");
                }
                var prLinks = sb.ToString();

                jsonDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.RESTAPIReviews",
                    Value = sb.ToString()
                });

                var activeSpecPullRequest = ActiveSpecPullRequest;
                if (string.IsNullOrEmpty(activeSpecPullRequest))
                {
                    // If active spec pull request is not provided, use the first pull request from the list
                    activeSpecPullRequest = SpecPullRequests.FirstOrDefault() ?? string.Empty;
                }
                jsonDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ActiveSpecPullRequestUrl",
                    Value = activeSpecPullRequest
                });
            }
            
            if (!string.IsNullOrEmpty(title))
            {
                jsonDocument.Add(new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/System.Title",
                    Value = title
                });
            }

            return jsonDocument;
        }
    }
}
