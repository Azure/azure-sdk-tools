// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes;
using IssueLabelerService;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace IssueLabelerService
{
    /// <summary>
    /// MCP-specific RAG service with optimizations for MCP issue classification
    /// </summary>
    public class McpTriageRag
    {
        private readonly ILogger<McpTriageRag> _logger;
        private readonly TriageRag _triageRag;
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(1000);
        public McpTriageRag(
            ILogger<McpTriageRag> logger,
            TriageRag triageRag)
        {
            _logger = logger;
            _triageRag = triageRag;
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

            var results = await _triageRag.IssueTriageContentIndexAsync(
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
            try
            {
                var cleaned = query;
                
                cleaned = SafeRegex(cleaned, @"\be\.g\.?,?\s+\w+", "");
                cleaned = SafeRegex(cleaned, @"\b(like|such\s+as)\s+\w+", "");
                cleaned = SafeRegex(cleaned, @"\s*\(e\.g\.?,?\s+[^)]+\)", "");
                cleaned = SafeRegex(cleaned, @"\b(for\s+example|for\s+instance),?\s+\w+", "");
                cleaned = SafeRegex(cleaned, @"\s{2,}", " ").Trim();
                return cleaned.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error in CleanToolMentions, returning original query");
                return query;
            }   
        }

        /// <summary>
        /// Safely execute regex replace with timeout and exception handling
        /// </summary>
        private string SafeRegex(string input, string pattern, string replacement)
        {
            try
            {
                return Regex.Replace(input, pattern, replacement, RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning(ex, "Regex timeout for pattern: {Pattern}", pattern);
                return input;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid regex pattern '{Pattern}', skipping this replacement", pattern);
                return input;
            }
        }
    }
}
