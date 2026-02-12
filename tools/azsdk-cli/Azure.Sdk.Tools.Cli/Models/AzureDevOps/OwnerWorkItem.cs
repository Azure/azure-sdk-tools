// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Attributes;

namespace Azure.Sdk.Tools.Cli.Models.AzureDevOps;

/// <summary>
/// Represents an Owner work item from Azure DevOps.
/// </summary>
public class OwnerWorkItem : WorkItemBase
{
    [FieldName("Custom.GitHubAlias")]
    public string GitHubAlias { get; set; } = string.Empty;
}
