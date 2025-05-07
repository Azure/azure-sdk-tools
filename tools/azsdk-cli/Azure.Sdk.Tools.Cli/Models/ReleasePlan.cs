using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class ReleasePlan
    {
        public int WorkItemId { get; set; }
        public string WorkItemUrl { get; set; } = string.Empty;
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
        public List<SDKGenerationInfo> SDKGenerationInfos { get; set; } = [];

        public JsonPatchDocument GetPatchDocument()
        {
            // todo: why does this fail to compile when the code commented below is included?
            var jsonDocument = new JsonPatchDocument()
            {
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.ServiceTreeID",
                //    Value = ServiceTreeId
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.ProductServiceTreeID",
                //    Value = ProductTreeId
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.SDKReleaseMonth",
                //    Value = SDKReleaseMonth
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.MgmtScope",
                //    Value = IsManagementPlane ? "Yes" : "No"
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.DataScope",
                //    Value = IsDataPlane ? "Yes" : "No"
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.APISpecDefinitionType",
                //    Value = SpecType
                //},
                //new JsonPatchOperation
                //{
                //    Operation = Operation.Add,
                //    Path = "/fields/Custom.APISpecversion",
                //    Value = SpecAPIVersion
                //}
            };
            return jsonDocument;
        }

        public static string WrapSpecPullRequestAsHref(string pullRequest)
        {
            return $"<a href=\"{pullRequest}\">{pullRequest}</a>";
        }
    }

    public class SDKGenerationInfo
    {
        public string Language { get; set; } = string.Empty;
        public string GenerationPipelineUrl { get; set; } = string.Empty;
        public string SdkPullRequestUrl {  get; set; } = string.Empty;
    }
}
