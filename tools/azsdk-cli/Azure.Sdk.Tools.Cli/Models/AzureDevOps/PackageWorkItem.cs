// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Attributes;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps
{
    public class PackageWorkItem : WorkItemBase
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

        /// <summary>
        /// IDs of related work items (populated from work item relations).
        /// </summary>
        public HashSet<int> RelatedIds { get; set; } = [];

        /// <summary>
        /// Hydrated Owner references (populated after fetching all work items).
        /// </summary>
        public List<OwnerWorkItem> Owners { get; } = [];

        /// <summary>
        /// Hydrated Label references (populated after fetching all work items).
        /// </summary>
        public List<LabelWorkItem> Labels { get; } = [];

        /// <summary>
        /// Hydrated LabelOwner references (populated after fetching all work items).
        /// </summary>
        public List<LabelOwnerWorkItem> LabelOwners { get; } = [];
    }


}
