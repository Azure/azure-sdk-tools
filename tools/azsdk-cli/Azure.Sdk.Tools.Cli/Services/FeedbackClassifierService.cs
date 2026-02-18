// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.AI;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Helper class for classifying feedback items using LLM-powered classification.
/// Uses chunked batch LLM calls to classify items (default 8 per batch for accuracy),
/// then generates manual update guidance for FAILURE items.
/// </summary>
public class FeedbackClassifierService
{
    private readonly ICopilotAgentRunner _agentRunner;
    private readonly ILogger<FeedbackClassifierService> _logger;
    private readonly string _tspProjectPath;
    private readonly string _specRepoBasePath;
    private readonly string? _packagePath;
    private readonly ITypeSpecHelper _typeSpecHelper;
    private readonly int _batchSize;

    public const int DefaultBatchSize = 50;

    public FeedbackClassifierService(
        ICopilotAgentRunner agentRunner,
        ILoggerFactory loggerFactory,
        ITypeSpecHelper typeSpecHelper,
        string tspProjectPath,
        string? packagePath = null,
        int? batchSize = null)
    {
        _agentRunner = agentRunner;
        _logger = loggerFactory.CreateLogger<FeedbackClassifierService>();
        _typeSpecHelper = typeSpecHelper;
        _tspProjectPath = tspProjectPath;
        _specRepoBasePath = GetSpecRepoBasePath(tspProjectPath);
        _packagePath = packagePath;
        _batchSize = batchSize ?? DefaultBatchSize;
    }

    /// <summary>
    /// Classifies multiple feedback items in chunked batch LLM calls, then generates
    /// manual update guidance for FAILURE items individually.
    /// </summary>
    public async Task<bool> ClassifyAsync(
        List<FeedbackItem> customizableItems, 
        string globalContext, 
        string? language = null,
        string? serviceName = null,
        string? codeCustomizationDocUrl = null,
        CancellationToken ct = default)
    {
        if (customizableItems.Count == 0)
        {
            return true;
        }

        // Stage 1: Batch classify items in chunks for to balance accuracy and speed
        var chunks = customizableItems
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _batchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        _logger.LogInformation("Stage 1: Classifying {Count} items in {ChunkCount} batch(es) of up to {BatchSize} items", 
            customizableItems.Count, chunks.Count, _batchSize);

        // Load the TypeSpec customization guide once for all batches
        var referenceDocContent = await LoadTspCustomizationGuideAsync(ct);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            _logger.LogInformation("Processing batch {BatchNum}/{TotalBatches} ({ItemCount} items)", 
                i + 1, chunks.Count, chunk.Count);
            await BatchClassifyAsync(chunk, globalContext, language, serviceName, referenceDocContent, ct);
        }

        // Stage 2: Generate manual update guidance for FAILURE items in batch (single session)
        var failureItems = customizableItems.Where(i => i.Status == FeedbackStatus.FAILURE).ToList();
        if (failureItems.Count > 0)
        {
            _logger.LogInformation("Stage 2: Generating manual update guidance for {Count} FAILURE items in batch", failureItems.Count);
            await BatchGenerateManualUpdateGuidanceAsync(failureItems, language, codeCustomizationDocUrl, ct);
        }

        var allDone = customizableItems.All(i => i.Status != FeedbackStatus.TSP_APPLICABLE);
        return allDone;
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
    /// Stage 2: Generates manual update guidance for FAILURE items in a single batch call.
    /// When packagePath is available, provides file tools for inspecting SDK code.
    /// </summary>
    private async Task BatchGenerateManualUpdateGuidanceAsync(
        List<FeedbackItem> items,
        string? language,
        string? codeCustomizationDocUrl,
        CancellationToken ct)
    {
        try
        {
            var guidanceTemplate = new BatchManualUpdateGuidanceTemplate(
                items: items,
                language: language,
                codeCustomizationDocUrl: codeCustomizationDocUrl,
                packagePath: _packagePath
            );

            var guidancePrompt = guidanceTemplate.BuildPrompt();

            // Create file tools for SDK package inspection when path is available
            var tools = new List<AIFunction>();
            var maxIterations = 10;
            
            if (!string.IsNullOrEmpty(_packagePath))
            {
                tools.Add(FileTools.CreateReadFileTool(_packagePath));
                tools.Add(FileTools.CreateListFilesTool(_packagePath));
                tools.Add(FileTools.CreateGrepSearchTool(_packagePath));
                // Scale iterations based on item count - need more for file inspection
                maxIterations = Math.Min(10 + (items.Count * 5), 50);
            }

            var guidanceResult = await _agentRunner.RunAsync(new CopilotAgent<string>
            {
                Instructions = guidancePrompt,
                MaxIterations = maxIterations,
                Tools = tools
            }, ct);

            _logger.LogInformation("=== Batch Manual Update Guidance ({Count} items) ===", items.Count);
            _logger.LogInformation("{Result}", guidanceResult);
            _logger.LogInformation("=== End Batch Guidance ===");

            // Parse the batch result and apply guidance to each item
            ParseBatchGuidanceResult(items, guidanceResult, codeCustomizationDocUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate batch manual update guidance");
            // Fall back to default guidance for all items
            var defaultGuidance = !string.IsNullOrEmpty(codeCustomizationDocUrl)
                ? $"Manual code customization required. See: {codeCustomizationDocUrl}"
                : "Manual code customization required. TypeSpec decorators cannot address this feedback.";
            foreach (var item in items)
            {
                item.NextAction = defaultGuidance;
            }
        }
    }

    /// <summary>
    /// Parses the batch guidance result with ID-keyed blocks and applies guidance to items.
    /// Expected format:
    /// [item-id]
    /// guidance text...
    /// </summary>
    private void ParseBatchGuidanceResult(List<FeedbackItem> items, string result, string? codeCustomizationDocUrl)
    {
        var itemLookup = items.ToDictionary(i => i.Id, i => i);
        var matchedIds = new HashSet<string>();

        // Match blocks like: [some-guid-id]\n<guidance until next block or end>
        // Use a pattern that captures content until the next [id] block or end of string
        var blockPattern = new Regex(
            @"\[(?<id>[^\]]+)\]\s*\n(?<guidance>(?:(?!\n\[[^\]]+\]).)*)",
            RegexOptions.Singleline);

        foreach (Match match in blockPattern.Matches(result))
        {
            var id = match.Groups["id"].Value.Trim();
            var guidance = match.Groups["guidance"].Value.Trim();

            if (!itemLookup.TryGetValue(id, out var item))
            {
                _logger.LogWarning("Batch guidance result contains unknown item ID: {Id}", id);
                continue;
            }

            matchedIds.Add(id);
            item.NextAction = guidance;
            _logger.LogInformation("Item {Id} received guidance ({Length} chars)", id, guidance.Length);
        }

        // Handle any items that weren't in the response
        var defaultGuidance = !string.IsNullOrEmpty(codeCustomizationDocUrl)
            ? $"Manual code customization required. See: {codeCustomizationDocUrl}"
            : "Manual code customization required. TypeSpec decorators cannot address this feedback.";

        foreach (var item in items.Where(i => !matchedIds.Contains(i.Id)))
        {
            _logger.LogWarning("Item {Id} was not found in batch guidance result. Using default guidance.", item.Id);
            item.NextAction = defaultGuidance;
        }
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
