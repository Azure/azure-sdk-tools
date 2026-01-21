// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class ProductWorkItem : WorkItemBase
    {
        [FieldName("Custom.Language")]
        public string Language { get; set; } = string.Empty;

        [FieldName("Custom.Package")]
        public string PackageName { get; set; } = string.Empty;

        [FieldName("Custom.GroupId")]
        public string GroupId { get; set; } = string.Empty;

        [FieldName("Custom.PackageDisplayName")]
        public string PackageDisplayName { get; set; } = string.Empty;

        [FieldName("Custom.PackageType")]
        public string PackageType { get; set; } = string.Empty;

        [FieldName("Custom.PackageTypeNewLibrary")]
        public bool PackageTypeNewLibrary { get; set; } = false;

        [FieldName("Custom.PackageVersionMajorMinor")]
        public string PackageVersionMajorMinor { get; set; } = string.Empty;

        [FieldName("Custom.ServiceName")]
        public string ServiceName { get; set; } = string.Empty;

        [FieldName("Custom.PackageRepoPath")]
        public string PackageRepoPath { get; set; } = string.Empty;
    }
}
