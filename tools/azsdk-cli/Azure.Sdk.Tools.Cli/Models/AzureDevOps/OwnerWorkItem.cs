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

    /// <summary>
    /// The date/time when the owner was first detected as invalid.
    /// Null means the owner is currently considered valid.
    /// </summary>
    [FieldName("Custom.InvalidSince")]
    public DateTime? InvalidSince { get; set; }

    /// <summary>
    /// Whether this owner represents a GitHub team (e.g., azure/some-team) rather than an individual user.
    /// </summary>
    public bool IsGitHubTeam => GitHubAlias.Contains('/');

    /// <summary>
    /// When the owner is a team (e.g., azure/some-team), this list contains
    /// the expanded individual member aliases from the team user cache.
    /// </summary>
    public List<string> ExpandedMembers { get; set; } = [];

    /// <summary>
    /// IDs of related work items (populated from work item relations).
    /// </summary>
    public HashSet<int> RelatedIds { get; set; } = [];
}
