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
/// Uses a single batch LLM call to classify all items at once, then generates
/// manual update guidance for FAILURE items.
/// </summary>
public class FeedbackClassifierService
{
    private readonly ICopilotAgentRunner _agentRunner;
    private readonly ILogger<FeedbackClassifierService> _logger;
    private readonly string _tspProjectPath;
    private readonly string _specRepoBasePath;
    private readonly string? _packagePath;
    private readonly ITypeSpecHelper _typeSpecHelper;

    public const int MaxIterations = 4;

    public FeedbackClassifierService(
        ICopilotAgentRunner agentRunner,
        ILoggerFactory loggerFactory,
        ITypeSpecHelper typeSpecHelper,
        string tspProjectPath,
        string? packagePath = null)
    {
        _agentRunner = agentRunner;
        _logger = loggerFactory.CreateLogger<FeedbackClassifierService>();
        _typeSpecHelper = typeSpecHelper;
        _tspProjectPath = tspProjectPath;
        _specRepoBasePath = GetSpecRepoBasePath(tspProjectPath);
        _packagePath = packagePath;
    }

    /// <summary>
    /// Classifies multiple feedback items in a single batch LLM call, then generates
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

        // Stage 1: Batch classify all items in a single LLM call
        _logger.LogInformation("Stage 1: Batch classifying {Count} items in a single LLM call", customizableItems.Count);
        await BatchClassifyAsync(customizableItems, globalContext, language, serviceName, ct);

        // Stage 2: Generate manual update guidance for FAILURE items (no file tools for speed)
        var failureItems = customizableItems.Where(i => i.Status == FeedbackStatus.FAILURE).ToList();
        if (failureItems.Count > 0)
        {
            _logger.LogInformation("Stage 2: Generating manual update guidance for {Count} FAILURE items", failureItems.Count);
            foreach (var item in failureItems)
            {
                await GenerateManualUpdateGuidanceAsync(item, language, codeCustomizationDocUrl, ct);
            }
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
        CancellationToken ct)
    {
        var referenceDocContent = await LoadTspCustomizationGuideAsync(ct);

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
            MaxIterations = 50,
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
    /// Classification: PHASE_A | SUCCESS | FAILURE
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
        else if (classification.Contains("PHASE_A", StringComparison.OrdinalIgnoreCase) || 
                 classification.Contains("TSP_APPLICABLE", StringComparison.OrdinalIgnoreCase))
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
            item.Reason = reason;
            item.Context += $"\n\nClassification: {classification}\nReason: {reason}";
        }
        
        _logger.LogInformation("Item {Id} classified as {Status}: {Reason}", item.Id, status, reason);
    }

    /// <summary>
    /// Stage 2: Generates manual update guidance for FAILURE items.
    /// Runs without file tools (no package path) for speed.
    /// </summary>
    private async Task GenerateManualUpdateGuidanceAsync(
        FeedbackItem item,
        string? language,
        string? codeCustomizationDocUrl,
        CancellationToken ct)
    {
        try
        {
            var guidanceTemplate = new ManualUpdateGuidanceTemplate(
                feedbackText: item.Text,
                reason: item.Reason,
                language: language,
                codeCustomizationDocUrl: codeCustomizationDocUrl,
                packagePath: null // No package path — skip file tools for speed
            );

            var guidancePrompt = guidanceTemplate.BuildPrompt();

            var guidanceResult = await _agentRunner.RunAsync(new CopilotAgent<string>
            {
                Instructions = guidancePrompt,
                MaxIterations = 5, // No tools, so this should resolve in 1-2 iterations
                Tools = [] // No file tools for speed
            }, ct);

            _logger.LogInformation("=== Manual Update Guidance (Item {ItemId}) ===", item.Id);
            _logger.LogInformation("{Result}", guidanceResult);
            _logger.LogInformation("=== End Guidance ===");

            if (!string.IsNullOrWhiteSpace(guidanceResult))
            {
                item.NextAction = guidanceResult.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate manual update guidance for item {ItemId}", item.Id);
            item.NextAction = !string.IsNullOrEmpty(codeCustomizationDocUrl)
                ? $"Manual code customization required. See: {codeCustomizationDocUrl}"
                : "Manual code customization required. TypeSpec decorators cannot address this feedback.";
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
