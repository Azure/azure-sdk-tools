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
using IssueLabelerService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IssueLabelerService
{
    /// <summary>
    /// Experimental V2 labeler with structured tool descriptions instead of verbose rules.
    /// Hypothesis: Explicit label semantics will generalize better than few-shot examples.
    /// </summary>
    public class McpOpenAiLabelerV2 : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private readonly RepositoryConfiguration _config;
        private readonly McpTriageRag _ragService;
        private readonly BlobServiceClient _blobClient;

        public McpOpenAiLabelerV2(
            ILogger<LabelerFactory> logger,
            RepositoryConfiguration config,
            McpTriageRag ragService,
            BlobServiceClient blobClient)
        {
            _logger = logger;
            _config = config;
            _ragService = ragService;
            _blobClient = blobClient;
        }

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var modelName = _config.LabelModelName;

            var searchContentResults = await GetSearchContentResults(issue);

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
            string rawResult = await _ragService.SendMessageQnaAsync(
                instructions,
                userPrompt,
                modelName,
                contextBlock: null,
                structure: jsonSchema);

            if (string.IsNullOrWhiteSpace(rawResult))
            {
                _logger.LogInformation(
                    "OpenAI MCP labeler V2 returned empty response for issue #{IssueNumber} in {Repository}.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            JObject parsed;
            try
            {
                parsed = JObject.Parse(rawResult);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse OpenAI MCP labeler V2 JSON for issue #{IssueNumber} in {Repository}. Raw: {Raw}",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    rawResult);
                return new Dictionary<string, string>();
            }

            var result = ExtractAndValidateLabels(issue, parsed, serverLabels, toolLabels);

            if (result.Count == 0)
            {
                _logger.LogInformation(
                    "MCP labeler V2 produced no valid labels for issue #{IssueNumber} in {Repository}. Raw: {Raw}",
                    issue.IssueNumber,
                    issue.RepositoryName,
                    rawResult);
            }
            else
            {
                _logger.LogInformation(
                    "MCP labeler V2 result for issue #{IssueNumber} in {Repository}: {Labels}",
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
            var tagPattern = new System.Text.RegularExpressions.Regex(@"\[([A-Za-z]+)\]");
            var matches = tagPattern.Matches(title);

            foreach (System.Text.RegularExpressions.Match match in matches)
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
            - [Tool Description] → Use the specific tool whose description needs improvement
            - [Remote] → remote-mcp (remote MCP server connectivity)
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

            var query = $"Title: {issue.Title}\n\n{issue.Body ?? string.Empty}";
            _logger.LogInformation(
                "Searching MCP content index '{IndexName}' with query (V2): {Query}",
                indexName,
                query);

            var searchContentResults = await _ragService.SearchMcpIssuesAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                topK: top,
                scoreThreshold: scoreThreshold,
                cleanQuery: true,
                onlyLabeledIssues: true,
                excludeIssueId: $"microsoft/mcp/{issue.IssueNumber}/Issue");

            if (searchContentResults.Count == 0)
            {
                throw new InvalidDataException(
                    $"Not enough relevant MCP sources found for repository '{issue.RepositoryName}' issue #{issue.IssueNumber}.");
            }

            _logger.LogInformation(
                "Found {Count} MCP issues with score >= {Threshold} for issue #{IssueNumber} in {Repository} (V2).",
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

            return $@"
You are a GitHub issue classifier for the Azure MCP (Model Context Protocol) repository.

## TASK
Classify the issue into:
1. **Server label** (required): Which MCP server does this issue belong to?
2. **Tool label** (required for Azure MCP): Which specific tool/subsystem does this issue relate to?

**For other servers (Fabric, PowerBI, Template):** Tool label is typically OMITTED (set to null).

## SERVER LABELS
Choose exactly one: {serverLabelList}

## TOOL LABELS WITH DESCRIPTIONS
Read these carefully - each label has a specific scope:

{toolLabelDescriptions}

## FEW-SHOT EXAMPLES (learn from these)

Example 1: [CONSOLIDATED] always means tools-Core

Title:
[CONSOLIDATED] Some `get_azure_databases_details` prompts do not trigger the corresponding tool as expected

Description:
Multiple prompts across different Azure services fail to invoke their tools.
The database tool mentioned is only an example.

Correct classification:
Server: server-Azure.Mcp
Tool: tools-Core

Why:
The [CONSOLIDATED] tag means multiple tools or prompts are affected.
This is a server-level routing or dispatch issue, not a single service tool issue.

Example 2: Azure CLI tool behavior issues mean tools-AzCLI

Title:
Azure CLI returns incorrect subscription context despite successful invocation

Description:
The Azure CLI tool is invoked correctly and runs, but it returns incorrect
subscription or tenant information compared to the active az login context.
Other MCP tools work as expected.

Correct classification:
Server: server-Azure.Mcp
Tool: tools-AzCLI

Why:
The Azure CLI tool is selected and executed correctly, but its
Azure-CLI–specific behavior is incorrect.
This is not a routing, framework, or authentication infrastructure issue.

Example 3: VS Code extension issues mean tools-VSIX

Title:
Display warning message during install or first setup of Azure MCP extension
when organizational policies disable MCP

Description:
The issue occurs during installation or setup of the Azure MCP VS Code extension.

Correct classification:
Server: server-Azure.Mcp
Tool: tools-VSIX

Why:
The “Azure MCP extension” refers to the VS Code extension.
Extension installation or setup issues always belong to tools-VSIX,
not tools-Setup.

## HOW TO CLASSIFY (follow in order)
{tagHints}
**STEP 1: Check similar issues below**
Similar issues show real labeling decisions. If you find a close match, follow that pattern.

**STEP 2: Ask ""Who owns the fix?""**
Identify which team/component would need to change their code to fix this issue.
- If the fix is in ONE tool's code → use that tool's label
- If the fix is in server infrastructure (routing, dispatch, startup) → use tools-Core
- If [CONSOLIDATED] tag or issue affects MULTIPLE tools → use tools-Core

**STEP 3: Match to tool description**
Use the tool descriptions above to find the best semantic match.
- Ignore illustrative examples in the issue (""like KeyVault"", ""e.g., Storage"")
- Focus on the actual component being reported

Reasoning: We label based on WHAT the issue is ABOUT, not where code changes will go.
An onboarding request for ""Azure SQL"" is ABOUT SQL, so use tools-SQL label.

**STEP 4: When Multiple Tools Mentioned**
- Identify the PRIMARY failing component or requested feature
- Ignore tools mentioned as examples, comparisons, or in passing
- Example: ""Remove deplicated logging and telemetry operations"" → tools-Telemetry (where failure occurs)

**Key Distinction - tools-Core vs specific tools:**
- MCP client/server behavior issues, overall MCP subsystem issues, multiple tools affected → tools-Core  
- ONE tool returns wrong/empty data or has implementation bug → that specific tool
- ONE specific tool not triggered AND issue is NOT [CONSOLIDATED] (its description/registration issue) → that specific tool (eg: prompt does not trigger the `azmcp_get_bestpractices_get` tool -> tools-BestPractices)

## RESPONSE FORMAT
Return JSON only:
{{
  ""Server"": ""<server-label>"",
  ""Tool"": ""<tool-label>"",
  ""ServerConfidenceScore"": <0.0-1.0>,
  ""ToolConfidenceScore"": <0.0-1.0>
}}

## ISSUE TO CLASSIFY

**Title:** {issue.Title}

**Description:**
{issue.Body}

## SIMILAR ISSUES (for reference - learn from their labels)
{printableContext}
";
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

            if (string.IsNullOrEmpty(server))
            {
                _logger.LogWarning(
                    "MCP labeler V2 returned empty Server for issue #{IssueNumber} in {Repository}.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                return new Dictionary<string, string>();
            }

            if (!serverLabels.Contains(server, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "MCP labeler V2 returned invalid Server '{Server}' for issue #{IssueNumber} in {Repository}. Valid labels: {ValidLabels}",
                    server,
                    issue.IssueNumber,
                    issue.RepositoryName,
                    string.Join(", ", serverLabels));
                return new Dictionary<string, string>();
            }

            if (serverScore < confidenceThreshold)
            {
                _logger.LogInformation(
                    "MCP labeler V2 ServerConfidenceScore below threshold for issue #{IssueNumber} in {Repository}: {Score:F2} < {Threshold}. Skipping labeling.",
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
                    "MCP labeler V2 returned no Tool for issue #{IssueNumber} in {Repository}. Applying Server label only.",
                    issue.IssueNumber,
                    issue.RepositoryName);
                result["Server"] = server;
                return result;
            }

            bool toolIsUnknown = string.Equals(tool, "UNKNOWN", StringComparison.OrdinalIgnoreCase);

            if (!toolIsUnknown && !toolLabels.Contains(tool, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "MCP labeler V2 returned invalid Tool '{Tool}' for issue #{IssueNumber} in {Repository}. Valid labels: {ValidLabels}. Server label will still be applied.",
                    tool,
                    issue.IssueNumber,
                    issue.RepositoryName,
                    string.Join(", ", toolLabels.Take(10)) + (toolLabels.Count > 10 ? "..." : ""));
                toolIsUnknown = true;
            }

            if (!toolIsUnknown && toolScore < confidenceThreshold)
            {
                _logger.LogInformation(
                    "MCP labeler V2 ToolConfidenceScore below threshold for issue #{IssueNumber} in {Repository}: {Score:F2} < {Threshold}. Server label will still be applied.",
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
                "MCP labeler V2 predictions for issue #{IssueNumber}: Server='{Server}' ({ServerScore:F2}), Tool='{Tool}' ({ToolScore:F2})",
                issue.IssueNumber,
                server,
                serverScore,
                toolIsUnknown ? "UNKNOWN" : tool,
                toolScore);

            return result;
        }
        private class McpLabel
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

    }
}
