// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class ApiSpecWorkItem : WorkItemBase
    {
        [FieldName("Custom.APISpecversion")]
        public string SpecAPIVersion { get; set; } = string.Empty;

        [FieldName("Custom.APISpecDefinitionType")]
        public string SpecType {  get; set; } = string.Empty;

        public string ActiveSpecPullRequest { get; set; } = string.Empty;
        
        public List<string> SpecPullRequests { get; set; } = [];

        public override Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument GetPatchDocument()
        {
            var jsonDocument = base.GetPatchDocument();

            if (SpecPullRequests.Count > 0)
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
                jsonDocument.Add(new JsonPatchOperation
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
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ActiveSpecPullRequestUrl",
                    Value = activeSpecPullRequest
                });
            }

            return jsonDocument;
        }
    }
}
