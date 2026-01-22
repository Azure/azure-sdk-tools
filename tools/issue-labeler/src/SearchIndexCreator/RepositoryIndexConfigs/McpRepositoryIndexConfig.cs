// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Search.Documents.Indexes.Models;
using IssueLabeler.Shared;
using Octokit;

namespace SearchIndexCreator.RepositoryIndexConfigs
{
    /// <summary>
    /// Repository index configuration for MCP (Model Context Protocol) repositories.
    /// Uses Server/Tool labels identified by name prefix.
    /// </summary>
    public class McpRepositoryIndexConfig : IRepositoryIndexConfig
    {
        public string DisplayName => "MCP";
        public int MaxPageLength => 2200;
        public int PageOverlapLength => 250;

        public IEnumerable<InputFieldMappingEntry> GetCustomFieldMappings()
        {
            yield return new InputFieldMappingEntry("Server") { Source = "/document/Server" };
            yield return new InputFieldMappingEntry("Tool") { Source = "/document/Tool" };
        }

        // Issue retrieval configuration
        public ItemStateFilter IssueStateFilter => ItemStateFilter.All;
        public IEnumerable<string> RequiredLabels => Enumerable.Empty<string>();
        public int MinCommentLength => 150;
         // MCP doesn't fetch comments currently
        public bool IncludeComments => false;

        public (string? primary, string? secondary) AnalyzeLabels(IReadOnlyList<Octokit.Label> labels)
        {
            var serverLabels = labels.Where(IsServerLabel).Select(l => l.Name).ToList();
            var toolLabels = labels.Where(IsToolLabel).Select(l => l.Name).ToList();

            var serverLabel = serverLabels.Count > 0 ? string.Join(", ", serverLabels) : null;
            var toolLabel = toolLabels.Count > 0 ? string.Join(", ", toolLabels) : null;

            return (serverLabel, toolLabel);
        }

        public bool ShouldSkipIssue(string? primaryLabel, string? secondaryLabel)
        {
            // MCP includes all issues regardless of labels
            return false;
        }

        public string FormatBody(Issue issue)
        {
            // MCP prepends title to body for better context
            return string.IsNullOrWhiteSpace(issue.Body)
                ? $"Title: {issue.Title}\n\n[No description provided]"
                : $"Title: {issue.Title}\n\n{issue.Body}";
        }

        public void PopulateCustomFields(IssueTriageContent content, string? primaryLabel, string? secondaryLabel)
        {
            content.Server = primaryLabel;
            content.Tool = secondaryLabel;
        }

        public IEnumerable<string> GetCodeowners(IReadOnlyList<string> labels)
        {
            return CodeOwnerUtils.GetCodeownersEntryForLabelList(labels.ToList()).ServiceOwners;
        }

        private static bool IsServerLabel(Octokit.Label label) =>
            label.Name.StartsWith("server-", StringComparison.OrdinalIgnoreCase);

        private static bool IsToolLabel(Octokit.Label label) =>
            label.Name.StartsWith("tools-", StringComparison.OrdinalIgnoreCase) ||
            label.Name.Equals("remote-mcp", StringComparison.OrdinalIgnoreCase);
    }
}
