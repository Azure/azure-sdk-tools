// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using Azure.Sdk.Tools.Cli.Attributes;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class WorkItemBase
    {
        public int WorkItemId { get; set; }

        public string WorkItemUrl { get; set; } = string.Empty;

        public string WorkItemHtmlUrl { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime ChangedDate { get; set; }

        public bool IsCreatedByAgent { get; set; } = false;

        [FieldName("System.AssignedTo")]
        public string AssignedTo { get; set; } = string.Empty;

        [FieldName("System.Tags")]
        public string Tag { get; set; } = string.Empty;

        [FieldName("System.Title")]
        public string Title { get; set; } = string.Empty;

        [FieldName("System.Parent")]
        public int ParentId { get; set; } = 0;

        public string Description { get; set; } = string.Empty;

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

                var value = prop.GetValue(this);

                // Convert boolean values to "Yes"/"No"
                if (value is bool boolValue)
                {
                    value = boolValue ? "Yes" : "No";
                }

                jsonDocument.Add(new JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = $"/fields/{attr.Name}",
                    Value = value
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

