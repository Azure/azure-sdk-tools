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
    /// Uses Service/Category labels identified by color (pink/yellow).
    /// </summary>
    public class AzureSdkRepositoryIndexConfig : IRepositoryIndexConfig
    {
        public string DisplayName => "Azure SDK";

        // Skillset configuration
        public int MaxPageLength => 1000;
        public int PageOverlapLength => 100;

        public IEnumerable<InputFieldMappingEntry> GetCustomFieldMappings()
        {
            yield return new InputFieldMappingEntry("Service") { Source = "/document/Service" };
            yield return new InputFieldMappingEntry("Category") { Source = "/document/Category" };
        }

        // Issue retrieval configuration
        public ItemStateFilter IssueStateFilter => ItemStateFilter.Closed;
        public IEnumerable<string> RequiredLabels => new[] { "customer-reported", "issue-addressed" };
        public int MinCommentLength => 250;
        public bool IncludeComments => true;

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

        public bool ShouldSkipIssue(string? primaryLabel, string? secondaryLabel)
        {
            // Azure SDK requires both service and category labels
            return primaryLabel == null || secondaryLabel == null;
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

        public IEnumerable<string> GetCodeowners(IReadOnlyList<string> labels)
        {
            return CodeOwnerUtils.GetCodeownersEntryForLabelList(labels.ToList()).AzureSdkOwners;
        }

        private static bool IsServiceLabel(Octokit.Label label) =>
            string.Equals(label.Color, "e99695", StringComparison.InvariantCultureIgnoreCase);

        private static bool IsCategoryLabel(Octokit.Label label) =>
            string.Equals(label.Color, "ffeb77", StringComparison.InvariantCultureIgnoreCase);
    }
}
