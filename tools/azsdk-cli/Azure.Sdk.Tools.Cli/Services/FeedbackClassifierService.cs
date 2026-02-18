// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Classifies feedback items using LLM-powered classification.
/// Uses chunked batch LLM calls to classify items as TSP_APPLICABLE, SUCCESS, or FAILURE.
/// </summary>
public class FeedbackClassifierService
{
    private readonly ICopilotAgentRunner _agentRunner;
    private readonly ILogger<FeedbackClassifierService> _logger;
    private readonly string _tspProjectPath;
    private readonly string _specRepoBasePath;
    private readonly ITypeSpecHelper _typeSpecHelper;
    private readonly int _batchSize;

    public const int DefaultBatchSize = 50;

    public FeedbackClassifierService(
        ICopilotAgentRunner agentRunner,
        ILoggerFactory loggerFactory,
        ITypeSpecHelper typeSpecHelper,
        string tspProjectPath,
        int? batchSize = null)
    {
        _agentRunner = agentRunner;
        _logger = loggerFactory.CreateLogger<FeedbackClassifierService>();
        _typeSpecHelper = typeSpecHelper;
        _tspProjectPath = tspProjectPath;
        _specRepoBasePath = GetSpecRepoBasePath(tspProjectPath);
        _batchSize = batchSize ?? DefaultBatchSize;
    }

    /// <summary>
    /// Classifies feedback items in chunked batch LLM calls. Mutates items in place.
    /// </summary>
    public async Task<FeedbackClassificationResponse> ClassifyItemsAsync(
        List<FeedbackItem> items, 
        string globalContext, 
        string? language = null,
        string? serviceName = null,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return new FeedbackClassificationResponse { Message = "No items to classify.", Classifications = [] };
        }

        var chunks = items
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _batchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        _logger.LogInformation("Classifying {Count} items in {ChunkCount} batch(es) of up to {BatchSize} items", 
            items.Count, chunks.Count, _batchSize);

        var referenceDocContent = await LoadTspCustomizationGuideAsync(ct);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            _logger.LogInformation("Processing batch {BatchNum}/{TotalBatches} ({ItemCount} items)", 
                i + 1, chunks.Count, chunk.Count);
            await BatchClassifyAsync(chunk, globalContext, language, serviceName, referenceDocContent, ct);
        }
        return BuildClassificationResponse(items);
    }

    private static FeedbackClassificationResponse BuildClassificationResponse(List<FeedbackItem> items)
    {
        var successCount = 0;
        var failureCount = 0;
        var tspApplicableCount = 0;
        var classifications = new List<FeedbackClassificationResponse.ItemClassificationDetails>();

        foreach (var item in items)
        {
            var classification = item.Status switch
            {
                FeedbackStatus.SUCCESS => "SUCCESS",
                FeedbackStatus.FAILURE => "FAILURE",
                _ => "TSP_APPLICABLE"
            };

            switch (item.Status)
            {
                case FeedbackStatus.SUCCESS: successCount++; break;
                case FeedbackStatus.FAILURE: failureCount++; break;
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
            Message = $"Classification complete: {successCount} success, {failureCount} failure, {tspApplicableCount} tsp-applicable",
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
            FileTools.CreateReadFileTool(_specRepoBasePath),
            FileTools.CreateListFilesTool(_specRepoBasePath),
            FileTools.CreateGrepSearchTool(_specRepoBasePath)
        };

        var result = await _agentRunner.RunAsync(new CopilotAgent<string>
        {
            Instructions = prompt,
            MaxIterations = 25,
            Tools = specTools
        }, ct);

        _logger.LogInformation("=== Batch Classification Result ===");
        _logger.LogInformation("{Result}", result);
        _logger.LogInformation("=== End Batch Result ===");

        // Parse the batch result and apply classifications to each item
        ParseBatchResult(items, result);
    }

    /// <summary>
    /// Parses the batch LLM result with ID-keyed blocks and applies classifications to items.
    /// Expected format:
    /// [item-id]
    /// Classification: TSP_APPLICABLE | SUCCESS | FAILURE
    /// Reason: explanation
    /// </summary>
    private void ParseBatchResult(List<FeedbackItem> items, string result)
    {
        var itemLookup = items.ToDictionary(i => i.Id, i => i);
        var matchedIds = new HashSet<string>();

        // Match blocks like: [some-guid-id]\nClassification: ...\nReason: ...
        var blockPattern = new Regex(
            @"\[(?<id>[^\]]+)\]\s*\n\s*Classification:\s*(?<classification>\S+)\s*\n\s*Reason:\s*(?<reason>.+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        foreach (Match match in blockPattern.Matches(result))
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
            _logger.LogWarning("Item {Id} was not found in batch classification result. Defaulting to FAILURE.", item.Id);
            item.Status = FeedbackStatus.FAILURE;
            item.Context += "\nClassification failed: item missing from batch LLM response";
        }
    }

    /// <summary>
    /// Applies a classification string and reason to a single feedback item.
    /// </summary>
    private void ApplyClassification(FeedbackItem item, string classification, string reason)
    {
        FeedbackStatus status;
        if (classification.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            status = FeedbackStatus.SUCCESS;
        }
        else if (classification.Contains("FAILURE", StringComparison.OrdinalIgnoreCase))
        {
            status = FeedbackStatus.FAILURE;
        }
        else if (classification.Contains("TSP_APPLICABLE", StringComparison.OrdinalIgnoreCase))
        {
            status = FeedbackStatus.TSP_APPLICABLE;
        }
        else
        {
            _logger.LogWarning("Unknown classification '{Classification}' for item {Id}. Defaulting to TSP_APPLICABLE.", classification, item.Id);
            status = FeedbackStatus.TSP_APPLICABLE;
        }

        item.Status = status;
        if (!string.IsNullOrEmpty(reason))
        {
            item.ClassificationReason = reason;
            item.Context += $"\n\nClassification: {classification}\nReason: {reason}";
        }
        
        _logger.LogInformation("Item {Id} classified as {Status}: {Reason}", item.Id, status, reason);
    }

    /// <summary>
    /// Resolves the spec repo base path by walking up from tspProjectPath.
    /// </summary>
    private string GetSpecRepoBasePath(string tspProjectPath)
    {
        var repoRoot = _typeSpecHelper.GetSpecRepoRootPath(tspProjectPath);
        if (string.IsNullOrEmpty(repoRoot))
        {
            throw new ArgumentException(
                $"Could not determine spec repo root from TypeSpec project path: {tspProjectPath}",
                nameof(tspProjectPath));
        }
        return repoRoot;
    }

    /// <summary>
    /// Loads the TypeSpec client customization guide from eng/common/knowledge/customizing-client-tsp.md.
    /// </summary>
    private async Task<string> LoadTspCustomizationGuideAsync(CancellationToken ct)
    {
        var guidePath = Path.Combine(_specRepoBasePath, "eng", "common", "knowledge", "customizing-client-tsp.md");
        if (!File.Exists(guidePath))
        {
            throw new FileNotFoundException(
                $"TypeSpec client customization guide not found at: {guidePath}. " +
                "Ensure the azure-rest-api-specs repository is up to date.",
                guidePath);
        }
        return await File.ReadAllTextAsync(guidePath, ct);
    }
}
