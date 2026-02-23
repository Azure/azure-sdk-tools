// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps;

/// <summary>
/// Represents a Label work item from Azure DevOps.
/// </summary>
public class LabelWorkItem : WorkItemBase
{
    [FieldName("Custom.Label")]
    public string LabelName { get; set; } = string.Empty;
}
