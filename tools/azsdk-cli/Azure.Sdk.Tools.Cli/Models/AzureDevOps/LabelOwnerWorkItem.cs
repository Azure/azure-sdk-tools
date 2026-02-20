// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps;

/// <summary>
/// Represents a Label Owner work item from Azure DevOps.
/// </summary>
public class LabelOwnerWorkItem : WorkItemBase
{
    [FieldName("Custom.LabelType")]
    public string LabelType { get; set; } = string.Empty;

    [FieldName("Custom.Repository")]
    public string Repository { get; set; } = string.Empty;

    [FieldName("Custom.RepoPath")]
    public string RepoPath { get; set; } = string.Empty;

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
}
