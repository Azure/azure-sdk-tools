// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using IssueLabelerService;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IssueLabelerService
{
    /// <summary>
    /// MCP-specific RAG service with optimizations for MCP issue classification
    /// </summary>
    public class McpTriageRag : TriageRag
    {
        private readonly ILogger<McpTriageRag> _logger;

        public McpTriageRag(
            ILogger<McpTriageRag> logger,
            AzureOpenAIClient openAiClient,
            SearchIndexClient searchIndexClient)
            : base(logger, openAiClient, searchIndexClient)
        {
            _logger = logger;
        }

        /// <summary>
        /// Search for similar MCP issues with query preprocessing and automatic filtering
        /// </summary>
        public async Task<List<IndexContent>> SearchMcpIssuesAsync(
            string indexName,
            string semanticConfigName,
            string fieldName,
            string query,
            int topK = 10,
            double scoreThreshold = 0.8,
            bool cleanQuery = true,
            bool onlyLabeledIssues = true,
            string excludeIssueId = null)
        {
            string filter = null;
            var processedQuery = cleanQuery ? CleanToolMentions(query) : query;

            if (cleanQuery && processedQuery != query)
            {
                _logger.LogDebug("Original: {Original}", query.Substring(0, Math.Min(100, query.Length)));
                _logger.LogDebug("Cleaned: {Cleaned}", processedQuery.Substring(0, Math.Min(100, processedQuery.Length)));
            }
            if (onlyLabeledIssues)
            {
                filter = "DocumentType eq 'Issue' and Server ne null";
                
                // Exclude the current issue being predicted to prevent data leakage
                if (!string.IsNullOrEmpty(excludeIssueId))
                {
                    filter += $" and Id ne '{excludeIssueId}'";
                    _logger.LogDebug("Excluding issue {IssueId} from search results", excludeIssueId);
                }
                
                _logger.LogDebug("Retrieving labeled MCP issues (Server label required, Tool optional)");
            }

            var results = await IssueTriageContentIndexAsync(
                indexName,
                semanticConfigName,
                fieldName,
                processedQuery,
                topK,
                scoreThreshold,
                filter: filter);

            _logger.LogInformation(
                "Found {Count} MCP issues with score >= {Threshold}",
                results.Count,
                scoreThreshold);

            return results;
        }

        /// <summary>
        /// Remove tool name mentions that appear as examples
        /// </summary>
        private string CleanToolMentions(string query)
        {
            var cleaned = Regex.Replace(query, @"\be\.g\.?,?\s+\w+", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\b(like|such\s+as)\s+\w+", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s*\(e\.g\.?,?\s+[^)]+\)", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\b(for\s+example|for\s+instance),?\s+\w+", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            return cleaned;
        }

        /// <summary>
        /// Get MCP-specific search statistics for debugging
        /// </summary>
        public async Task<McpSearchStats> GetSearchStatsAsync(
            string indexName,
            string semanticConfigName,
            string fieldName,
            string query,
            int topK = 50)
        {
            var stats = new McpSearchStats();

            // Get raw results without filtering
            var allResults = await AzureSearchQueryAsync<IndexContent>(
                indexName,
                semanticConfigName,
                fieldName,
                query,
                topK,
                filter: null);

            stats.TotalResults = allResults.Count;

            foreach (var (issue, score) in allResults)
            {
                if (issue.Server != null && issue.Tool != null)
                    stats.LabeledIssues++;
                else
                    stats.UnlabeledIssues++;

                if (score >= 0.8)
                    stats.HighScoreResults++;

                stats.Scores.Add(score);
            }

            return stats;
        }
    }

    /// <summary>
    /// Statistics about MCP search results for debugging and optimization
    /// </summary>
    public class McpSearchStats
    {
        public int TotalResults { get; set; }
        public int LabeledIssues { get; set; }
        public int UnlabeledIssues { get; set; }
        public int HighScoreResults { get; set; }
        public List<double> Scores { get; set; } = new List<double>();

        public double AverageScore => Scores.Count > 0 ? Scores.Average() : 0;
        public double MaxScore => Scores.Count > 0 ? Scores.Max() : 0;
        public double MinScore => Scores.Count > 0 ? Scores.Min() : 0;

        public override string ToString()
        {
            return $"Total: {TotalResults}, Labeled: {LabeledIssues}, Unlabeled: {UnlabeledIssues}, " +
                   $"High Score (>=0.8): {HighScoreResults}, Avg Score: {AverageScore:F3}";
        }
    }
}
