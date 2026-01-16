// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
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
                var prLinks = string.Join("<br>", SpecPullRequests.Select(pr => $"<a href=\"{pr}\">{pr}</a>"));
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.RESTAPIReviews",
                    Value = prLinks
                });
                
                var activeSpec = string.IsNullOrEmpty(ActiveSpecPullRequest)
                    ? SpecPullRequests.FirstOrDefault() ?? string.Empty
                    : ActiveSpecPullRequest;
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.ActiveSpecPullRequestUrl",
                    Value = activeSpec
                });
            }

            return jsonDocument;
        }
    }
}
