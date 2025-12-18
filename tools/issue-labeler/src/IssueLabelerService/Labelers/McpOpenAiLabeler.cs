// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using IssueLabeler.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IssueLabelerService
{
    /// <summary>
    /// MCP-specific RAG + few-shot based labeler.
    /// Predicts:
    ///   - Service (server-*)
    ///   - Tool   (tools-* or UNKNOWN)
    /// using Azure Search + Azure OpenAI with JSON schema-constrained output.
    /// </summary>
    public class McpOpenAiLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private readonly RepositoryConfiguration _config;
        private readonly TriageRag _ragService;
        private readonly BlobServiceClient _blobClient;

        public McpOpenAiLabeler(
            ILogger<LabelerFactory> logger,
            RepositoryConfiguration config,
            TriageRag ragService,
            BlobServiceClient blobClient)
        {
            _logger = logger;
            _config = config;
            _ragService = ragService;
            _blobClient = blobClient;
        }

        /// <summary>
        /// Predicts labels for an MCP issue:
        ///   - Service  (one of the server-* labels)
        ///   - Tool     (one of the tools-* labels or UNKNOWN)
        /// </summary>
        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var modelName = _config.LabelModelName;

            // 1. Retrieve grounding context from the MCP search index
            var searchContentResults = await GetSearchContentResults(issue);

            // 2. Load MCP labels (Server + Tool) from blob
            var allLabels = (await GetMcpLabelsAsync(issue.RepositoryName)).ToList();
            var serverLabels = allLabels
                .Where(l => string.Equals(l.Type, "Server", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toolLabels = allLabels
                .Where(l => string.Equals(l.Type, "Tool", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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

            // 3. Build prompt content
            string printableContext = BuildPrintableContext(searchContentResults);
            string serverLabelList = string.Join(", ", serverLabels);
            string toolLabelList = string.Join(", ", toolLabels);

            string userPrompt = BuildUserPrompt(
                issue,
                serverLabelList,
                toolLabelList,
                printableContext);

            // 4. Build JSON schema for structured output
            var jsonSchema = BuildJsonSchema();

            // 5. Call OpenAI via TriageRag
            var instructions = _config.LabelInstructions; // You can point this to an MCP-specific instruction string
            string rawResult = await _ragService.SendMessageQnaAsync(
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

            // 6. Parse JSON and validate
            JObject parsed;
            try
            {
                parsed = JObject.Parse(rawResult);
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

        #region Label DTO

        private class McpLabel
        {
            public string Name { get; set; }
            public string Type { get; set; } // "Server" or "Tool"
        }

        #endregion

        #region Labels + Context Retrieval

        private async Task<IEnumerable<McpLabel>> GetMcpLabelsAsync(string repositoryName)
        {
            var containerClient = _blobClient.GetBlobContainerClient("labels");
            var blobClient = containerClient.GetBlobClient(repositoryName);

            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException(
                    $"Label blob for repository '{repositoryName}' not found in 'labels' container.");
            }

            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();

            var labels = JsonConvert.DeserializeObject<IEnumerable<McpLabel>>(json);

            if (labels == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize MCP labels for repository '{repositoryName}'.");
            }

            return labels;
        }

        private async Task<List<IndexContent>> GetSearchContentResults(IssuePayload issue)
        {
            var indexName = _config.IndexName;
            var semanticName = _config.SemanticName;
            var top = int.Parse(_config.SourceCount, CultureInfo.InvariantCulture);
            var scoreThreshold = double.Parse(_config.ScoreThreshold, CultureInfo.InvariantCulture);
            var fieldName = _config.IssueIndexFieldName;

            // Format query to match indexed document structure for better similarity
            var query = $"Title: {issue.Title}\n\n{issue.Body ?? string.Empty}";
            _logger.LogInformation(
                "Searching MCP content index '{IndexName}' with query: {Query}",
                indexName,
                query);

            var searchContentResults = await _ragService.IssueTriageContentIndexAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                top,
                scoreThreshold,
                labels: null,
                filter: "DocumentType eq 'Issue' and Server ne null and Tool ne null");

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
                $"URL: {sc.Url}\n" +
                $"Score: {sc.Score:F2}"));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Extract tags like [ONBOARD], [CONSOLIDATED], [BUG] from issue title
        /// </summary>
        private string ExtractTags(string title)
        {
            var tags = new List<string>();
            var tagPattern = new System.Text.RegularExpressions.Regex(@"\[([A-Z]+)\]");
            var matches = tagPattern.Matches(title);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                tags.Add(match.Groups[1].Value);
            }
            
            return tags.Any() ? string.Join(", ", tags) : "None";
        }

        #endregion

        #region Prompt + Schema

        private string BuildUserPrompt(
            IssuePayload issue,
            string serverLabelList,
            string toolLabelList,
            string printableContext)
        {
            var tags = ExtractTags(issue.Title);
            var tagHint = tags != "None" ? $@"

⚠️ DETECTED TAGS: {tags}
Tag-Based Hints:
- [ONBOARD]: Issue requesting a NEW tool/service → Use the SPECIFIC tool label being requested (not tools-Core)
- [CONSOLIDATED]: Multiple issues combined → Focus on the PRIMARY failure point
- [BUG]: Bug in existing functionality → Use the tool where the bug occurs
- [BUGBASH]: Testing/QA issue → Analyze the actual failure, not the test process
" : string.Empty;

            return $@"
You are an assistant that classifies GitHub issues for the MCP (Model Context Protocol) repository.

CRITICAL REQUIREMENTS:
- You MUST provide BOTH a Server label AND a Tool label for EVERY issue.
- Both labels are mandatory, even if the context shows some issues as ""Unlabeled"".
- The unlabeled issues in the context are historical data before labeling - ignore their missing labels.
{tagHint}
Your task:
1. Choose a single **Server** label for the issue.
   - It MUST be exactly one of the following server labels:
     {serverLabelList}
   - This is MANDATORY. Every issue requires a Server label.

2. Choose a single **Tool** label for the issue.
   - It MUST be one of the following tool labels:
     {toolLabelList}
   - If the issue does not clearly relate to any specific tool, use the literal value ""UNKNOWN"".
   - Providing Tool = ""UNKNOWN"" is acceptable when truly uncertain, but prefer selecting a real tool when possible.

CRITICAL CLASSIFICATION RULES:

**Rule 1: Ignore Tool Names Used as Examples**
- If an issue says ""like KeyVault"" or ""e.g., Storage changes"", these are EXAMPLES, not the actual component.
- Focus on what is ACTUALLY broken or being requested, not what is mentioned for illustration.
- Example: ""Enable MCP to query service updates (e.g., Key Vault changes)"" → tools-Core (new feature), NOT tools-KeyVault

**Rule 2: Understanding tools-Core - The Layer Principle**

Think of the MCP system as having 3 layers:
1. **Infrastructure Layer** (tools-Core): Server, protocol, routing, packaging
2. **Tool Layer** (specific tools): Individual tool implementations
3. **External Layer**: Azure services, user code, network

Use tools-Core when the problem is in **Layer 1** (infrastructure):
- Server won't start or crashes (before any tool runs)
- Installation/packaging problems (npx, Docker, extensions, packages)
- Protocol/transport issues (stdio, handshake, capability negotiation)
- Configuration/environment affecting ALL tools (env vars, config files, proxy settings)
- Server-wide settings: namespace mode, consolidated mode configuration
- Framework issues: telemetry framework, server-wide authentication setup

Use specific tool label when the problem is in **Layer 2** (tool execution):
- The tool WAS invoked but produced wrong results
- Tool-specific authentication, permissions, or API errors
- Tool-specific timeout, data parsing, or business logic errors
- Tool not being invoked when it SHOULD be (tool's own routing/registration issue)

KEY DISTINCTION - ""Prompt doesn't trigger tool X""
This is AMBIGUOUS - context matters:
- If issue mentions """"[CONSOLIDATED]"""" or """"consolidated mode"""" → likely tools-Core (mode configuration)
- If issue is about ONE specific tool in normal mode → likely that specific tool (tool registration/implementation)
- If issue says """"prompt triggers WRONG tool"""" or """"uses CLI instead of MCP"""" → tools-Core (server routing)
- Default: Assume it's the TOOL's issue unless clear evidence of server-level routing problem

**Rule 2a: Scope Test - Infrastructure vs Tool**
Ask: ""Would this affect MULTIPLE tools or just ONE?""
- Affects ALL tools → tools-Core (infrastructure scope)
- Affects ONE tool → specific tool (tool scope)
- Ambiguous → Check error message: generic system error → Core, specific API/data error → tool

Examples applying the scope test:
- ""Proxy timeout"" → ALL tools affected → tools-Core
- ""KeyVault invalid credential"" → ONE tool → tools-KeyVault  
- ""Environment variable missing"" → ALL tools → tools-Core
- ""Cosmos query timeout"" → ONE tool → tools-CosmosDb

**Rule 3: Onboarding and Feature Requests**
For requests to ADD new functionality:
- If requesting a NEW tool/service → Use the tool label for that service (even if not yet implemented)
- If requesting infrastructure feature → tools-Core
- Look for tags: [ONBOARD] usually means new tool → use specific tool label

Reasoning: We label based on WHAT the issue is ABOUT, not where code changes will go.
An onboarding request for ""Azure SQL"" is ABOUT SQL, so use tools-SQL label.

**Rule 4: When Multiple Tools Mentioned**
- Identify the PRIMARY failing component or requested feature
- Ignore tools mentioned as examples, comparisons, or in passing
- Example: ""Cosmos insert fails after Storage download"" → tools-CosmosDb (where failure occurs)

**CRITICAL EXAMPLES - Study These Carefully:**

Example 1: Authentication failure in a specific tool
- Issue: ""KeyVault tool authentication fails with DefaultAzureCredential error""
- WRONG: tools-Auth (too generic)
- RIGHT: tools-KeyVault (auth failure happens WHEN using KeyVault tool)

Example 2: Telemetry service failure
- Issue: ""Server crashes due to telemetry initialization error""
- WRONG: tools-Core (seems like infrastructure)
- RIGHT: tools-Telemetry (telemetry is a specific tool/service, not Core)

Example 3: Generic startup without specific tool
- Issue: ""MCP server won't start, TypeScript error in server.ts""
- WRONG: Any specific tool
- RIGHT: tools-Core (no specific tool mentioned, generic server issue)

Example 4: All tools affected
- Issue: ""All tools fail with proxy ETIMEDOUT error""
- WRONG: Specific tool
- RIGHT: tools-Core (explicitly states ALL tools affected)

Guidelines:
- **Prioritize the retrieved similar issues** - they show real examples of how issues were classified
- Use the issue Title and Description as the primary signal
- Apply common sense reasoning about where the problem actually occurs
- If similar issues show ""Unlabeled"" for Server/Tool, ignore that - you must still provide labels
- Prefer the most specific label that matches the problem domain
- If multiple tools could apply, pick the one most central to the error or feature request
- Look at error messages, stack traces, and file paths - they reveal where the problem is

Confidence Scoring:
- Set ServerConfidenceScore based on how certain you are about the Server label (0.0 to 1.0).
- Set ToolConfidenceScore based on how certain you are about the Tool label (0.0 to 1.0).
- Use ""UNKNOWN"" for Tool only if confidence would be extremely low (<0.6).

Return:
- A JSON object with exactly these fields:
  - ""Server"": the chosen server label (string, REQUIRED)
  - ""Tool"": the chosen tool label or ""UNKNOWN"" (string, REQUIRED)
  - ""ServerConfidenceScore"": a number between 0 and 1 (REQUIRED)
  - ""ToolConfidenceScore"": a number between 0 and 1 (REQUIRED)

Do NOT include any extra fields or text outside of the JSON.

Issue to classify:
Title:
{issue.Title}

Description:
{issue.Body}

Retrieved similar issues and context (note: ""Unlabeled"" entries are historical issues awaiting classification):
{printableContext}
";
        }

        private static BinaryData BuildJsonSchema()
        {
            // Schema for:
            // {
            //   "Server": "server-Azure.Mcp",
            //   "Tool": "tools-kusto",
            //   "ServerConfidenceScore": 0.93,
            //   "ToolConfidenceScore": 0.82
            // }
            var schemaJson = @"
{
  ""type"": ""object"",
  ""properties"": {
    ""Server"": {
      ""type"": ""string""
    },
    ""Tool"": {
      ""type"": ""string""
    },
    ""ServerConfidenceScore"": {
      ""type"": ""number""
    },
    ""ToolConfidenceScore"": {
      ""type"": ""number""
    }
  },
  ""required"": [ ""Server"", ""Tool"" ],
  ""additionalProperties"": false
}
";
            return BinaryData.FromString(schemaJson);
        }

        #endregion

        #region Validation

        private Dictionary<string, string> ExtractAndValidateLabels(
            IssuePayload issue,
            JObject parsed,
            List<string> serverLabels,
            List<string> toolLabels)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string server = parsed.Value<string>("Server") ?? string.Empty;
            string tool = parsed.Value<string>("Tool") ?? string.Empty;

            double serverScore = parsed.Value<double?>("ServerConfidenceScore") ?? 0.0;
            double toolScore = parsed.Value<double?>("ToolConfidenceScore") ?? 0.0;

            var confidenceThreshold = double.Parse(
                _config.ConfidenceThreshold,
                CultureInfo.InvariantCulture);

            // 1. Validate Server
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

            // 2. Validate Tool
            if (string.IsNullOrEmpty(tool))
            {
                _logger.LogWarning(
                    "MCP labeler returned empty Tool for issue #{IssueNumber} in {Repository}.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                return new Dictionary<string, string>();
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
                // Treat invalid tool as UNKNOWN - still apply Server label
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
                // Treat low-confidence tool as UNKNOWN - still apply Server label
                toolIsUnknown = true;
            }

            // 3. Build final result - only include Tool if it's not UNKNOWN
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

        #endregion
    }
}
 