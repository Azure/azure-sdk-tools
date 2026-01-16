// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class EpicWorkItem : WorkItemBase
    {
        [FieldName("Custom.PackageDisplayName")]
        public string PackageDisplayName { get; set; } = string.Empty;

        [FieldName("Custom.ServiceName")]
        public string ServiceName { get; set; } = string.Empty;

        [FieldName("Custom.EpicType")]
        public string EpicType { get; set; } = string.Empty;
    }
}
