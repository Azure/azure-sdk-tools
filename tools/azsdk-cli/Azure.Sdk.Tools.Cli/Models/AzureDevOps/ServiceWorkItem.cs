// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class ServiceWorkItem : WorkItemBase
    {
        [FieldName("Custom.PackageDisplayName")]
        public string PackageDisplayName { get; set; } = string.Empty;

        [FieldName("Custom.ServiceName")]
        public string ServiceName { get; set; } = string.Empty;

        [FieldName("Custom.EpicType")]
        public string EpicType { get; set; } = string.Empty;
    }
}
