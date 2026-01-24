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

    public class McpOpenAiLabeler : ILabeler
    {
        private readonly ILogger<LabelerFactory> _logger;
        private readonly RepositoryConfiguration _config;
        private readonly McpTriageRag _ragService;
        private readonly BlobServiceClient _blobClient;

        public McpOpenAiLabeler(
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
            string toolLabelList = string.Join(", ", toolLabels);

            string userPrompt = BuildUserPrompt(
                issue,
                serverLabelList,
                toolLabelList,
                printableContext);

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
            public string Type { get; set; }
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

            var query = $"Title: {issue.Title}\n\n{issue.Body ?? string.Empty}";
            _logger.LogInformation(
                "Searching MCP content index '{IndexName}' with query: {Query}",
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
                excludeIssueId: issue.IssueNumber.ToString()); 

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

DETECTED TAGS: {tags}
Tag-Based Hints:
- [ONBOARD]: Issue requesting a NEW tool/service → Use the SPECIFIC tool label being requested (not tools-Core)
- [CONSOLIDATED]: Multiple issues combined → Focus on the PRIMARY failure point
- [BUG]: Bug in existing functionality → Use the tool where the bug occurs
- [BUGBASH]: Testing/QA issue → Analyze the actual failure, not the test process
" : string.Empty;

            return $@"
You are an assistant that classifies GitHub issues for the MCP (Model Context Protocol) repository.

CRITICAL REQUIREMENTS:
- You MUST provide a Server label for EVERY issue.
- Tool label requirements depend on which server:
  * For server-Azure.Mcp: You MUST also provide a Tool label (mandatory)
  * For other servers (Fabric, PowerBI, Template): Tool label is OPTIONAL
- If the context shows some issues as ""Unlabeled"", ignore that - those are historical data before labeling.
{tagHint}
Your task:
1. Choose a single **Server** label for the issue.
   - It MUST be exactly one of the following server labels:
     {serverLabelList}
   - This is MANDATORY. Every issue requires a Server label.

2. Choose a **Tool** label for the issue (requirements vary by server):
   - Available tool labels:
     {toolLabelList}
   
   For server-Azure.Mcp issues:
   - Tool label is MANDATORY
   - Secondary label rules for server-azure-mcp:
    - Use a tools-* label ONLY when the issue is about a specific MCP tool
    (e.g. Storage, CosmosDB, EventGrid).
    - Use remote-mcp label when the issue is about:
    - remote MCP routing
    - proxying
    - topology
    - remote clusters
    - authentication or networking specific to remote MCP
    - remote-mcp is NOT a tool. It replaces the tool label when applicable.
   - If the issue does not clearly relate to any specific tool, use the literal value ""UNKNOWN""
   - Providing Tool = ""UNKNOWN"" is acceptable when truly uncertain, but prefer selecting a real tool when possible
   
   **For other servers (Fabric, PowerBI, Template):**
   - Tool label is OPTIONAL and typically should be OMITTED
   - These servers currently have no tool taxonomy defined
   - Only provide a Tool label if the issue explicitly involves an Azure tool (rare edge case)
   - In most cases, set Tool to null or omit it from the response

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
- ""Kusto timeout"" → ONE tool → tools-Kusto

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

**AUTHORITATIVE CLASSIFICATION ANCHORS (FEW-SHOT):**

These examples illustrate boundary cases. Do NOT assume new issues match them unless the same reasoning applies.

Example A:
Title: ""[CONSOLIDATED] Some get_azure_best_practices prompts do not trigger the corresponding tool""
Description: ""The prompt resolves, but the expected tool is not invoked.""

Output:
{{
  ""Server"": ""server-Azure.Mcp"",
  ""Tool"": ""tools-Core"",
  ""ServerConfidenceScore"": 0.95,
  ""ToolConfidenceScore"": 0.9
}}

Example B:
Title: ""Azure MCP on Claude Desktop: Unable to get list of VMs from the Azure Tenant""
Description: ""The request relies on Azure CLI authentication and az commands.""

Output:
{{
  ""Server"": ""server-Azure.Mcp"",
  ""Tool"": ""tools-AzCLI"",
  ""ServerConfidenceScore"": 0.95,
  ""ToolConfidenceScore"": 0.9
}}

Example C:
Title: ""subscription list tools generate responses that are too large""
Description: ""Issue occurs while listing subscription inventory.""

Output:
{{
  ""Server"": ""server-Azure.Mcp"",
  ""Tool"": ""tools-ARM"",
  ""ServerConfidenceScore"": 0.9,
  ""ToolConfidenceScore"": 0.85
}}

**CONCEPTUAL EXAMPLES (FOR UNDERSTANDING ONLY — NOT EXHAUSTIVE):**

The following examples are illustrative abstractions and do not correspond to any specific real issue.

Example 1: Authentication failure scoped to a single tool
- Issue: ""A specific resource-oriented tool fails authentication when invoking cloud credentials.""
- WRONG: tools-Auth (too generic)
- RIGHT: The specific tool involved (authentication failed during that tool’s execution)

Principle:
Authentication errors belong to the tool if they occur after the tool is invoked, even if shared credentials are used.

Example 2: Failure in a named internal service used by the server
- Issue: ""The server terminates during startup due to an initialization failure in an internal service.""
- WRONG: tools-Core (appears infrastructural at first glance)
- RIGHT: The internal service’s tool label

Principle:
If a failure originates in a specific internal service or subsystem, label that service rather than Core.

Example 3: Server startup failure with no tool involvement
- Issue: ""The server fails to start due to a runtime or configuration error before any tools are invoked.""
- WRONG: Any specific tool
- RIGHT: tools-Core

Principle:
When no tool execution occurs, the issue belongs to infrastructure (Core).

Example 4: Cross-cutting failure affecting all tools
- Issue: ""Every tool invocation fails due to a shared environmental or connectivity issue.""
- WRONG: Any single tool
- RIGHT: tools-Core

Principle:
Failures that impact multiple or all tools simultaneously indicate an infrastructure-level problem.

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
  - ""Tool"": the chosen tool label or ""UNKNOWN"" (string, REQUIRED for Azure MCP, OPTIONAL for others)
  - ""ServerConfidenceScore"": a number between 0 and 1 (REQUIRED)
  - ""ToolConfidenceScore"": a number between 0 and 1 (REQUIRED if Tool is provided, otherwise optional)

Notes:
- For Azure MCP issues, always include Tool (even if ""UNKNOWN"")
- For other servers, you may omit Tool or set it to null if not applicable

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
            bool isAzureMcp = server.Equals("server-Azure.Mcp", StringComparison.OrdinalIgnoreCase);
            
            if (string.IsNullOrEmpty(tool))
            {
                // if (isAzureMcp)
                // {
                //     _logger.LogWarning(
                //         "MCP labeler returned empty Tool for Azure MCP issue #{IssueNumber} in {Repository}. Tool is required for Azure MCP.",
                //         issue.IssueNumber,
                //         issue.RepositoryName);
                //     return new Dictionary<string, string>();
                // }
                // else
                // {
                    _logger.LogInformation(
                        "MCP labeler returned no Tool for issue #{IssueNumber} in {Repository}. This is acceptable for non-Azure MCP servers. Applying Server label only.",
                        issue.IssueNumber,
                        issue.RepositoryName);
                    result["Server"] = server;
                    return result;
                //}
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
 