// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents.Indexes.Models;
using IssueLabeler.Shared;
using Octokit;

namespace SearchIndexCreator.RepositoryIndexConfigs
{
    /// <summary>
    /// Defines repository-specific configuration for issue retrieval and indexing.
    /// Implement this interface to add support for new repository types.
    /// </summary>
    public interface IRepositoryIndexConfig
    {
        /// <summary>
        /// Display name for logging purposes.
        /// </summary>
        string DisplayName { get; }

        // ===== Skillset/Indexing Configuration =====

        /// <summary>
        /// Maximum page length for text chunking in the search skillset.
        /// </summary>
        int MaxPageLength { get; }

        /// <summary>
        /// Page overlap length for text chunking in the search skillset.
        /// </summary>
        int PageOverlapLength { get; }

        /// <summary>
        /// Gets the custom field mappings for this repo type (e.g., Service/Category or Server/Tool).
        /// </summary>
        IEnumerable<InputFieldMappingEntry> GetCustomFieldMappings();

        // ===== Issue Retrieval Configuration =====

        /// <summary>
        /// Issue state filter for GitHub API requests.
        /// </summary>
        ItemStateFilter IssueStateFilter { get; }

        /// <summary>
        /// Labels required on issues to be included in retrieval.
        /// </summary>
        IEnumerable<string> RequiredLabels { get; }

        /// <summary>
        /// Minimum comment body length to include in results.
        /// </summary>
        int MinCommentLength { get; }

        /// <summary>
        /// Whether to include issue comments in the retrieval.
        /// </summary>
        bool IncludeComments { get; }

        /// <summary>
        /// Analyzes issue labels and returns primary and secondary label values.
        /// Returns (primaryLabel, secondaryLabel) - field names depend on repo type.
        /// </summary>
        (string? primary, string? secondary) AnalyzeLabels(IReadOnlyList<Octokit.Label> labels);

        /// <summary>
        /// Determines if an issue should be skipped based on label analysis results.
        /// </summary>
        bool ShouldSkipIssue(string? primaryLabel, string? secondaryLabel);

        /// <summary>
        /// Formats the issue body for indexing.
        /// </summary>
        string FormatBody(Issue issue);

        /// <summary>
        /// Populates the repo-specific fields on the IssueTriageContent object.
        /// </summary>
        void PopulateCustomFields(IssueTriageContent content, string? primaryLabel, string? secondaryLabel);

        /// <summary>
        /// Gets the codeowners property name to use (e.g., AzureSdkOwners or ServiceOwners).
        /// </summary>
        IEnumerable<string> GetCodeowners(IReadOnlyList<string> labels);
    }
}
