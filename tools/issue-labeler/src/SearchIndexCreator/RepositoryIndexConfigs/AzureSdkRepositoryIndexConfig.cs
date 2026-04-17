// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Search.Documents.Indexes.Models;
using IssueLabeler.Shared;
using Octokit;

namespace SearchIndexCreator.RepositoryIndexConfigs
{
    /// <summary>
    /// Repository index configuration for Azure SDK repositories.
    /// </summary>
    public class AzureSdkRepositoryIndexConfig : IRepositoryIndexConfig
    {
        private const int DefaultMaxPageLength = 1000;
        private const int DefaultPageOverlapLength = 100;
        private const int DefaultMinCommentLength = 250;

        public string DisplayName => "Azure SDK";

        // Skillset configuration
        public int MaxPageLength => DefaultMaxPageLength;
        public int PageOverlapLength => DefaultPageOverlapLength;

        public IEnumerable<InputFieldMappingEntry> GetCustomFieldMappings()
        {
            yield return new InputFieldMappingEntry("Service") { Source = "/document/Service" };
            yield return new InputFieldMappingEntry("Category") { Source = "/document/Category" };
        }

        // Issue retrieval configuration
        public ItemStateFilter IssueStateFilter => ItemStateFilter.Closed;
        public IEnumerable<string> RequiredLabels => new[] { "customer-reported", "issue-addressed" };
        public int MinCommentLength => DefaultMinCommentLength;
        public bool IncludeComments => true;
        public bool SkipPullRequests => false;

        public (string? primary, string? secondary) AnalyzeLabels(IReadOnlyList<Octokit.Label> labels)
        {
            Octokit.Label? service = null;
            Octokit.Label? category = null;

            foreach (var label in labels)
            {
                if (IsServiceLabel(label))
                    service = label;
                else if (IsCategoryLabel(label))
                    category = label;
            }

            return (service?.Name, category?.Name);
        }

        public bool ShouldSkipIssue(IReadOnlyList<Octokit.Label> labels, string? primaryLabel, string? secondaryLabel)
        {
            if (primaryLabel == null || secondaryLabel == null)
                return true;

            var hasCustomerReported = labels.Any(l => l.Name.Equals("customer-reported", StringComparison.OrdinalIgnoreCase));
            var hasIssueAddressed = labels.Any(l => l.Name.Equals("issue-addressed", StringComparison.OrdinalIgnoreCase));

            return !hasCustomerReported || !hasIssueAddressed;
        }

        public string FormatBody(Issue issue)
        {
            return issue.Body ?? string.Empty;
        }

        public void PopulateCustomFields(IssueTriageContent content, string? primaryLabel, string? secondaryLabel)
        {
            content.Service = primaryLabel;
            content.Category = secondaryLabel;
        }

        public IEnumerable<string> GetCodeowners(List<string> labels)
        {
            return CodeOwnerUtils.GetCodeownersEntryForLabelList(labels).AzureSdkOwners;
        }

        private static bool IsServiceLabel(Octokit.Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Octokit.Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);
    }
}
