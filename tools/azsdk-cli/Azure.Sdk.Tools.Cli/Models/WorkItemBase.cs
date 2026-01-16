// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class WorkItemBase
    {
        public int WorkItemId { get; set; }

        public string WorkItemUrl { get; set; } = string.Empty;

        public string WorkItemHtmlUrl { get; set; } = string.Empty;

        public bool IsCreatedByAgent { get; set; } = false;

        [FieldName("System.AssignedTo")]
        public string AssignedTo { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;

        [FieldName("System.Title")]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [FieldName("System.State")]
        public string Status { get; set; } = string.Empty;

        [FieldName("Custom.PrimaryPM")]
        public string Owner { get; set; } = string.Empty;
        
        public virtual Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument GetPatchDocument()
        {
            var jsonDocument = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument();

            // Add all fields with FieldName attribute
            foreach (var prop in GetType().GetProperties())
            {
                var attr = prop.GetCustomAttribute<FieldNameAttribute>();
                if (attr == null)
                {
                    continue;
                }
 
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = $"/fields/{attr.Name}",
                    Value = prop.GetValue(this)
                });
            }

            if (IsCreatedByAgent)
            {
                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/fields/Custom.CreatedUsing",
                    Value = "Copilot"
                });
            }

            return jsonDocument;
        }
    }
}

