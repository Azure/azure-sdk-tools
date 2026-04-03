// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureSdkKnowledgeAICompletion;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Classifies feedback items using LLM-powered classification.
/// </summary>
public interface IFeedbackClassifierService
{
    /// <summary>
    /// Classifies feedback items in chunked batch LLM calls. Mutates items in place.
    /// </summary>
    Task<FeedbackClassificationResponse> ClassifyItemsAsync(
        List<FeedbackItem> items,
        string globalContext,
        string tspProjectPath,
        string? language = null,
        string? serviceName = null,
        int? batchSize = null,
        CancellationToken ct = default);
}

/// <summary>
/// Classifies feedback items using LLM-powered classification.
/// Uses chunked batch LLM calls to classify items as TSP_APPLICABLE, SUCCESS, or REQUIRES_MANUAL_INTERVENTION.
/// </summary>
public class FeedbackClassifierService : IFeedbackClassifierService
{
    private static readonly ConcurrentDictionary<string, string> TspCustomizationGuideCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex BatchResultBlockPattern = new(
        @"\[(?<id>[^\]]+)\]\s*\n\s*Classification:\s*(?<classification>\S+)\s*\n\s*Reason:\s*(?<reason>[^\n]+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ICopilotAgentRunner _agentRunner;
    private readonly ILogger<FeedbackClassifierService> _logger;
    private readonly ITypeSpecHelper _typeSpecHelper;
    private readonly IAzureSdkKnowledgeBaseService? _knowledgeBaseService;

    public const int DefaultBatchSize = 50;

    public FeedbackClassifierService(
        ICopilotAgentRunner agentRunner,
        ILoggerFactory loggerFactory,
        ITypeSpecHelper typeSpecHelper,
        IAzureSdkKnowledgeBaseService? knowledgeBaseService = null)
    {
        _agentRunner = agentRunner;
        _logger = loggerFactory.CreateLogger<FeedbackClassifierService>();
        _typeSpecHelper = typeSpecHelper;
        _knowledgeBaseService = knowledgeBaseService;
    }

    /// <summary>
    /// Classifies feedback items in chunked batch LLM calls. Mutates items in place.
    /// </summary>
    public async Task<FeedbackClassificationResponse> ClassifyItemsAsync(
        List<FeedbackItem> items,
        string globalContext,
        string tspProjectPath,
        string? language = null,
        string? serviceName = null,
        int? batchSize = null,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("No feedback items to classify. Provide an APIView URL or plain text feedback.");
        }

        var specRepoBasePath = _typeSpecHelper.GetSpecRepoRootPath(tspProjectPath);
        if (string.IsNullOrEmpty(specRepoBasePath))
        {
            throw new ArgumentException(
                $"Could not determine spec repo root from TypeSpec project path: {tspProjectPath}",
                nameof(tspProjectPath));
        }

        var effectiveBatchSize = batchSize ?? DefaultBatchSize;
        _logger.LogInformation("Classifying {Count} items in batch(es) of up to {BatchSize} items",
            items.Count, effectiveBatchSize);

        var referenceDocContent = await LoadTspCustomizationGuideAsync(specRepoBasePath, ct);

        // Enrich with RAG context if the knowledge base service is available.
        // This combines the local KB file (complete decorator reference) with
        // broader TypeSpec knowledge from the RAG-indexed documentation.
        var ragContext = await QueryRagContextAsync(items, ct);
        if (!string.IsNullOrEmpty(ragContext))
        {
            referenceDocContent += "\n\n## Additional TypeSpec Authoring Context (RAG)\n\n" + ragContext;
            _logger.LogInformation("Enriched classifier context with RAG knowledge base content");
        }

        foreach (var chunk in items.Chunk(effectiveBatchSize))
        {
            await BatchClassifyAsync(chunk.ToList(), globalContext, language, serviceName, referenceDocContent, specRepoBasePath, ct);
        }
        return BuildClassificationResponse(items);
    }

    private static FeedbackClassificationResponse BuildClassificationResponse(List<FeedbackItem> items)
    {
        var successCount = 0;
        var failureCount = 0;
        var tspApplicableCount = 0;
        var codeCustomizationCount = 0;
        var classifications = new List<FeedbackClassificationResponse.ItemClassificationDetails>();

        foreach (var item in items)
        {
            var classification = item.Status switch
            {
                FeedbackStatus.SUCCESS => "SUCCESS",
                FeedbackStatus.CODE_CUSTOMIZATION => "CODE_CUSTOMIZATION",
                FeedbackStatus.REQUIRES_MANUAL_INTERVENTION => "REQUIRES_MANUAL_INTERVENTION",
                _ => "TSP_APPLICABLE"
            };

            switch (item.Status)
            {
                case FeedbackStatus.SUCCESS: successCount++; break;
                case FeedbackStatus.CODE_CUSTOMIZATION: codeCustomizationCount++; break;
                case FeedbackStatus.REQUIRES_MANUAL_INTERVENTION: failureCount++; break;
                default: tspApplicableCount++; break;
            }

            classifications.Add(new FeedbackClassificationResponse.ItemClassificationDetails
            {
                ItemId = item.Id,
                Classification = classification,
                Reason = item.ClassificationReason ?? $"Item classified as {classification}",
                Text = item.Text
            });
        }

        return new FeedbackClassificationResponse
        {
            Message = $"Classification complete: {successCount} success, {tspApplicableCount} tsp-applicable, {codeCustomizationCount} code-customization, {failureCount} requires-manual-intervention",
            Classifications = classifications
        };
    }

    /// <summary>
    /// Sends all feedback items to the LLM in a single batch call and parses the
    /// ID-keyed response to assign classifications.
    /// </summary>
    private async Task BatchClassifyAsync(
        List<FeedbackItem> items,
        string globalContext,
        string? language,
        string? serviceName,
        string referenceDocContent,
        string specRepoBasePath,
        CancellationToken ct)
    {
        var template = new FeedbackClassificationTemplate(
            serviceName: serviceName,
            language: language,
            referenceDocContent: referenceDocContent,
            items: items,
            globalContext: globalContext
        );

        var prompt = template.BuildPrompt();

        // Tools scoped to spec repo for TypeSpec project inspection
        var specTools = new List<AIFunction>
        {
            FileTools.CreateReadFileTool(specRepoBasePath),
            FileTools.CreateListFilesTool(specRepoBasePath),
            FileTools.CreateGrepSearchTool(specRepoBasePath)
        };

        var result = await _agentRunner.RunAsync(new CopilotAgent<string>
        {
            Instructions = prompt,
            MaxIterations = 25,
            Tools = specTools
        }, ct);

        _logger.LogDebug("=== Batch Classification Result ===");
        _logger.LogDebug("{Result}", result);
        _logger.LogDebug("=== End Batch Result ===");

        // Parse the batch result and apply classifications to each item
        ParseBatchResult(items, result);
    }

    /// <summary>
    /// Parses the batch LLM result with ID-keyed blocks and applies classifications to items.
    /// Expected format:
    /// [item-id]
    /// Classification: TSP_APPLICABLE | SUCCESS | CODE_CUSTOMIZATION | REQUIRES_MANUAL_INTERVENTION
    /// Reason: explanation
    /// </summary>
    private void ParseBatchResult(List<FeedbackItem> items, string result)
    {
        var itemLookup = items.ToDictionary(i => i.Id, i => i);
        var matchedIds = new HashSet<string>();

        foreach (Match match in BatchResultBlockPattern.Matches(result))
        {
            var id = match.Groups["id"].Value.Trim();
            var classification = match.Groups["classification"].Value.Trim();
            var reason = match.Groups["reason"].Value.Trim();

            if (!itemLookup.TryGetValue(id, out var item))
            {
                _logger.LogWarning("Batch result contains unknown item ID: {Id}", id);
                continue;
            }

            matchedIds.Add(id);
            ApplyClassification(item, classification, reason);
        }

        // Handle any items that weren't in the response
        foreach (var item in items.Where(i => !matchedIds.Contains(i.Id)))
        {
            _logger.LogWarning("Item {Id} was not found in batch classification result. Defaulting to REQUIRES_MANUAL_INTERVENTION.", item.Id);
            item.Status = FeedbackStatus.REQUIRES_MANUAL_INTERVENTION;
            item.AppendContext("Classification failed: item missing from batch LLM response", leadingNewLines: 1);
        }
    }

    /// <summary>
    /// Applies a classification string and reason to a single feedback item.
    /// </summary>
    private void ApplyClassification(FeedbackItem item, string classification, string reason)
    {
        var status = classification switch
        {
            "SUCCESS" => FeedbackStatus.SUCCESS,
            "CODE_CUSTOMIZATION" => FeedbackStatus.CODE_CUSTOMIZATION,
            "REQUIRES_MANUAL_INTERVENTION" => FeedbackStatus.REQUIRES_MANUAL_INTERVENTION,
            "TSP_APPLICABLE" => FeedbackStatus.TSP_APPLICABLE,
            _ => FeedbackStatus.TSP_APPLICABLE
        };

        if (status == FeedbackStatus.TSP_APPLICABLE && classification != "TSP_APPLICABLE")
        {
            _logger.LogWarning("Unknown classification '{Classification}' for item {Id}. Defaulting to TSP_APPLICABLE.", classification, item.Id);
        }

        item.Status = status;
        if (!string.IsNullOrEmpty(reason))
        {
            item.ClassificationReason = reason;
            item.AppendContext($"Classification: {classification}\nReason: {reason}", leadingNewLines: 2);
        }
        
        _logger.LogInformation("Item {Id} classified as {Status}: {Reason}", item.Id, status, reason);
    }

    /// <summary>
    /// Queries the RAG knowledge base for additional TypeSpec context relevant to the feedback items.
    /// Returns null if the knowledge base service is unavailable or the query fails.
    /// </summary>
    private async Task<string?> QueryRagContextAsync(List<FeedbackItem> items, CancellationToken ct)
    {
        if (_knowledgeBaseService == null)
        {
            _logger.LogDebug("RAG knowledge base service not available, skipping RAG enrichment");
            return null;
        }

        try
        {
            // Build a focused query from the feedback items
            var feedbackSummary = string.Join("; ",
                items.Take(5).Select(i => i.Text.Length > 100 ? i.Text[..100] : i.Text));

            var request = new CompletionRequest
            {
                AzureSdkKnowledgeServiceTenant = AzureSdkKnowledgeServiceTenant.AzureTypespecAuthoring,
                Message = new Message
                {
                    Role = Role.User,
                    Content = $"What TypeSpec decorators, patterns, or customizations are relevant for these SDK feedback items? " +
                              $"Focus on client.tsp customization options and known limitations. Feedback: {feedbackSummary}"
                },
                WithAgenticSearch = true
            };

            var response = await _knowledgeBaseService.SendCompletionRequestAsync(request, ct);

            if (!response.HasResult || string.IsNullOrEmpty(response.Answer))
            {
                _logger.LogDebug("RAG query returned no results");
                return null;
            }

            _logger.LogDebug("RAG query returned {RefCount} references", response.References?.Count ?? 0);
            return response.FullContext ?? response.Answer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG knowledge base query failed, continuing with local KB only");
            return null;
        }
    }

    /// <summary>
    /// Loads the TypeSpec client customization guide from eng/common/knowledge/customizing-client-tsp.md.
    /// </summary>
    private async Task<string> LoadTspCustomizationGuideAsync(string specRepoBasePath, CancellationToken ct)
    {
        var guidePath = Path.Combine(specRepoBasePath, "eng", "common", "knowledge", "customizing-client-tsp.md");

        if (TspCustomizationGuideCache.TryGetValue(guidePath, out var cachedContent))
        {
            return cachedContent;
        }

        if (!File.Exists(guidePath))
        {
            throw new FileNotFoundException(
                $"TypeSpec client customization guide not found at: {guidePath}. " +
                "Ensure the azure-rest-api-specs repository is up to date.",
                guidePath);
        }

        var content = await File.ReadAllTextAsync(guidePath, ct);
        TspCustomizationGuideCache.TryAdd(guidePath, content);
        return content;
    }
}
