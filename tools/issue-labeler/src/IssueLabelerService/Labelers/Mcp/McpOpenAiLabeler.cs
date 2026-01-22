// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using IssueLabeler.Shared;
using IssueLabelerService;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IssueLabelerService
{
    public class McpOpenAiLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private readonly RepositoryConfiguration _config;
        private readonly TriageRag _triageRag;
        private readonly BlobServiceClient _blobClient;

        private static readonly ConcurrentDictionary<string, (List<McpLabel> Labels, DateTime FetchedAt)> _labelCache = new();
        private static readonly TimeSpan LabelCacheDuration = TimeSpan.FromHours(24);
        private static readonly Regex s_tagPattern = new(@"\[([A-Za-z]+)\]", RegexOptions.Compiled);
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(1000);

        public McpOpenAiLabeler(
            ILogger<LabelerFactory> logger,
            RepositoryConfiguration config,
            TriageRag triageRag,
            BlobServiceClient blobClient)
        {
            _logger = logger;
            _config = config;
            _triageRag = triageRag;
            _blobClient = blobClient;
        }

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var modelName = _config.LabelModelName;
            JsonDocument parsed;

            var searchTask = GetSearchContentResults(issue);
            var labelsTask = GetMcpLabelsAsync(issue.RepositoryName);

            await Task.WhenAll(searchTask, labelsTask);

            var searchContentResults = await searchTask;
            var allLabels = await labelsTask;

            var serverSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toolSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var label in allLabels)
            {
                if (string.Equals(label.Type, "Server", StringComparison.OrdinalIgnoreCase))
                {
                    serverSet.Add(label.Name);
                }
                else if (string.Equals(label.Type, "Tool", StringComparison.OrdinalIgnoreCase))
                {
                    toolSet.Add(label.Name);
                }
            }

            var serverLabels = serverSet.ToList();
            var toolLabels = toolSet.ToList();

            if (!serverLabels.Any())
            {
                _logger.LogWarning(
                    "No Server labels found for repository '{Repository}'. Skipping MCP labeling.",
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            if (!toolLabels.Any())
            {
                _logger.LogWarning(
                    "No Tool labels found for repository '{Repository}'. Skipping MCP labeling.",
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            string printableContext = BuildPrintableContext(searchContentResults);
            string serverLabelList = string.Join(", ", serverLabels);
            string toolLabelDescriptions = BuildToolLabelDescriptions(toolLabels);

            string userPrompt = BuildUserPrompt(
                issue,
                serverLabelList,
                toolLabelDescriptions,
                printableContext);

            var jsonSchema = BuildJsonSchema();

            var instructions = _config.LabelInstructions;
            string rawResult = await _triageRag.SendMessageQnaAsync(
                instructions,
                userPrompt,
                modelName,
                contextBlock: null,
                structure: jsonSchema);

            if (string.IsNullOrWhiteSpace(rawResult))
            {
                _logger.LogInformation(
                    "OpenAI MCP labeler returned empty response for issue #{IssueNumber} in {Repository}.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            try
            {
                parsed = JsonDocument.Parse(rawResult);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse OpenAI MCP labeler JSON for issue #{IssueNumber} in {Repository}. Raw: {Raw}",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    rawResult);
                return new Dictionary<string, string>();
            }

            var result = ExtractAndValidateLabels(issue, parsed, serverLabels, toolLabels);

            if (result.Count == 0)
            {
                _logger.LogInformation(
                    "MCP labeler produced no valid labels for issue #{IssueNumber} in {Repository}. Raw: {Raw}",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    rawResult);
            }
            else
            {
                _logger.LogInformation(
                    "MCP labeler result for issue #{IssueNumber} in {Repository}: {Labels}",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    string.Join(", ", result.Select(kv => $"{kv.Key}: {kv.Value}")));
            }

            return result;
        }

        /// <summary>
        /// Extract tags like [ONBOARD], [CONSOLIDATED], [BUG], [Remote] from issue title
        /// </summary>
        private static string ExtractTags(string title)
        {
            var tags = new List<string>();
            var matches = s_tagPattern.Matches(title);

            foreach (Match match in matches)
            {
                tags.Add(match.Groups[1].Value.ToUpperInvariant());
            }

            return tags.Any() ? string.Join(", ", tags) : "None";
        }

        /// <summary>
        /// Build tag-based hints for the prompt based on detected tags
        /// </summary>
        private static string BuildTagHints(string tags)
        {
            if (tags == "None") return string.Empty;

            return $@"**STEP 0: Apply tag rules (detected: {tags})**
            - [CONSOLIDATED] → ALWAYS tools-Core, regardless of tool names in the title. These track server-wide routing issues.
            - [ONBOARD] → Find the closest matching tool for the Azure service being requested
            - [BUG] / [BUGBASH] → Analyze where the bug actually occurs
            - [TOOL DESCRIPTION] → Use the specific tool whose description needs improvement
            - [REMOTE] → remote-mcp (remote MCP server connectivity)
            - [BESTPRACTICES] → tools-BestPractices
            ";
        }

        /// <summary>
        /// Build a formatted string of tool descriptions for labels that exist in the repository.
        /// </summary>
        private static string BuildToolLabelDescriptions(List<string> availableLabels)
        {
            var descriptions = new List<string>();

            foreach (var label in availableLabels.OrderBy(l => l))
            {
                if (McpToolDescription.ToolDescriptions.TryGetValue(label, out var description))
                {
                    descriptions.Add($"- **{label}**: {description}");
                }
                else
                {
                    // Label exists but no description - include with generic note
                    descriptions.Add($"- **{label}**: (No description available - use label name as guide)");
                }
            }

            return string.Join("\n", descriptions);
        }

        private async Task<IEnumerable<McpLabel>> GetMcpLabelsAsync(string repositoryName)
        {
            if (_labelCache.TryGetValue(repositoryName, out var cachedEntry) &&
                DateTime.UtcNow - cachedEntry.FetchedAt < LabelCacheDuration)
            {
                return cachedEntry.Labels;
            }
            var containerClient = _blobClient.GetBlobContainerClient("labels");
            var blobClient = containerClient.GetBlobClient(repositoryName);

            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException(
                    $"Label blob for repository '{repositoryName}' not found in 'labels' container.");
            }

            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();

            var labels = JsonSerializer.Deserialize<IEnumerable<McpLabel>>(json);
            if (labels == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize MCP labels for repository '{repositoryName}'.");
            }

            var labelList = labels.ToList();
            _labelCache[repositoryName] = (labelList, DateTime.UtcNow);
            return labelList;
        }

        private async Task<List<IndexContent>> GetSearchContentResults(IssuePayload issue)
        {
            var indexName = _config.IndexName;
            var semanticName = _config.SemanticName;
            var top = int.Parse(_config.SourceCount, CultureInfo.InvariantCulture);
            var scoreThreshold = double.Parse(_config.ScoreThreshold, CultureInfo.InvariantCulture);
            var fieldName = _config.IssueIndexFieldName;

            var rawQuery = $"Title: {issue.Title}\n\n{issue.Body ?? string.Empty}";
            var query = CleanToolMentions(rawQuery);
            
            if (query != rawQuery)
            {
                _logger.LogDebug("Query cleaned - Original: {Original}", rawQuery.Substring(0, Math.Min(100, rawQuery.Length)));
                _logger.LogDebug("Query cleaned - Cleaned: {Cleaned}", query.Substring(0, Math.Min(100, query.Length)));
            }

            _logger.LogInformation(
                "Searching MCP content index '{IndexName}' with query : {Query}",
                indexName,
                query);

            // Build filter for labeled issues, excluding current issue to prevent data leakage
            var excludeIssueId = $"microsoft/mcp/{issue.IssueNumber}/Issue";
            var filter = $"DocumentType eq 'Issue' and Server ne null and Id ne '{excludeIssueId}'";
            _logger.LogDebug("Retrieving labeled MCP issues (Server label required, Tool optional). Excluding issue {IssueId}", excludeIssueId);

            var searchContentResults = await _triageRag.IssueTriageContentIndexAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                top,
                scoreThreshold,
                filter);

            if (searchContentResults.Count == 0)
            {
                throw new InvalidDataException(
                    $"Not enough relevant MCP sources found for repository '{issue.RepositoryName}' issue #{issue.IssueNumber}.");
            }

            _logger.LogInformation(
                "Found {Count} MCP issues with score >= {Threshold} for issue #{IssueNumber} in {Repository}.",
                searchContentResults.Count,
                scoreThreshold,
                issue.IssueNumber,
                issue.RepositoryName);

            return searchContentResults;
        }

        private static string BuildPrintableContext(List<IndexContent> searchContentResults)
        {
            return string.Join("\n\n", searchContentResults.Select(sc =>
                $"Title: {sc.Title}\n" +
                $"Description: {sc.Chunk}\n" +
                $"Server: {sc.Server ?? "Unlabeled"}\n" +
                $"Tool: {sc.Tool ?? "Unlabeled"}\n" +
                $"Score: {sc.Score:F2}"));
        }

        private string BuildUserPrompt(
            IssuePayload issue,
            string serverLabelList,
            string toolLabelDescriptions,
            string printableContext)
        {
            var tags = ExtractTags(issue.Title);
            var tagHints = BuildTagHints(tags);

            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title ?? string.Empty },
                { "Description", issue.Body ?? string.Empty },
                { "TagHints", tagHints },
                { "ToolLabelDescriptions", toolLabelDescriptions },
                { "ServerLabelList", serverLabelList },
                { "PrintableContent", printableContext }
            };

            var userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.LabelPrompt, replacements, _logger);
            return userPrompt;
        }

        private static BinaryData BuildJsonSchema()
        {
            var schemaJson = @"
            {
            ""type"": ""object"",
            ""properties"": {
                ""Server"": {
                ""type"": ""string""
                },
                ""Tool"": {
                ""type"": [""string"", ""null""]
                },
                ""ServerConfidenceScore"": {
                ""type"": ""number""
                },
                ""ToolConfidenceScore"": {
                ""type"": [""number"", ""null""]
                }
            },
            ""required"": [ ""Server"", ""Tool"" ],
            ""additionalProperties"": false
            }
            ";
            return BinaryData.FromString(schemaJson);
        }

        private Dictionary<string, string> ExtractAndValidateLabels(
            IssuePayload issue,
            JsonDocument parsed,
            List<string> serverLabels,
            List<string> toolLabels)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var root = parsed.RootElement;
            string server = root.TryGetProperty("Server", out var serverProp) ? serverProp.GetString() ?? string.Empty : string.Empty;
            string tool = root.TryGetProperty("Tool", out var toolProp) ? toolProp.GetString() ?? string.Empty : string.Empty;

            double serverScore = root.TryGetProperty("ServerConfidenceScore", out var serverScoreProp) ? serverScoreProp.GetDouble() : 0.0;
            double toolScore = root.TryGetProperty("ToolConfidenceScore", out var toolScoreProp) ? toolScoreProp.GetDouble() : 0.0;

            var confidenceThreshold = double.Parse(
                _config.ConfidenceThreshold,
                CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(server))
            {
                _logger.LogWarning(
                    "MCP labeler returned empty Server for issue #{IssueNumber} in {Repository}.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            if (!serverLabels.Contains(server, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "MCP labeler returned invalid Server '{Server}' for issue #{IssueNumber} in {Repository}. Valid labels: {ValidLabels}",
                    server,
                    issue.IssueNumber,
                    issue.RepositoryName,
                    string.Join(", ", serverLabels));
                return new Dictionary<string, string>();
            }

            if (serverScore < confidenceThreshold)
            {
                _logger.LogInformation(
                    "MCP labeler ServerConfidenceScore below threshold for issue #{IssueNumber} in {Repository}: {Score:F2} < {Threshold}. Skipping labeling.",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    serverScore,
                    confidenceThreshold);
                return new Dictionary<string, string>();
            }

            bool isAzureMcp = server.Equals("server-Azure.Mcp", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(tool))
            {
                _logger.LogInformation(
                    "MCP labeler returned no Tool for issue #{IssueNumber} in {Repository}. Applying Server label only.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                result["Server"] = server;
                return result;
            }

            bool toolIsUnknown = string.Equals(tool, "UNKNOWN", StringComparison.OrdinalIgnoreCase);

            if (!toolIsUnknown && !toolLabels.Contains(tool, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "MCP labeler returned invalid Tool '{Tool}' for issue #{IssueNumber} in {Repository}. Valid labels: {ValidLabels}. Server label will still be applied.",
                    tool,
                    issue.IssueNumber,
                    issue.RepositoryName,
                    string.Join(", ", toolLabels.Take(10)) + (toolLabels.Count > 10 ? "..." : ""));
                toolIsUnknown = true;
            }

            if (!toolIsUnknown && toolScore < confidenceThreshold)
            {
                _logger.LogInformation(
                    "MCP labeler ToolConfidenceScore below threshold for issue #{IssueNumber} in {Repository}: {Score:F2} < {Threshold}. Server label will still be applied.",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    toolScore,
                    confidenceThreshold);
                toolIsUnknown = true;
            }

            result["Server"] = server;

            if (!toolIsUnknown)
            {
                result["Tool"] = tool;
            }

            _logger.LogInformation(
                "MCP labeler predictions for issue #{IssueNumber}: Server='{Server}' ({ServerScore:F2}), Tool='{Tool}' ({ToolScore:F2})",
                issue.IssueNumber,
                server,
                serverScore,
                toolIsUnknown ? "UNKNOWN" : tool,
                toolScore);

            return result;
        }

        /// <summary>
        /// Remove tool name mentions that appear as examples to improve search quality
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

        private class McpLabel
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

    }
}
