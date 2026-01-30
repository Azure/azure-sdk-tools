// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Prompts.Templates;
using Microsoft.Extensions.Configuration;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Helper class for classifying feedback items using LLM-powered classification.
/// Determines if feedback is actionable and provides next steps.
/// </summary>
public class FeedbackClassifierService
{
    private readonly IMicroagentHostService _microagentHost;
    private readonly ILogger<FeedbackClassifierService> _logger;
    private readonly string _model;
    private readonly string? _tspProjectPath;
    private readonly string? _packagePath;

    public const int MaxIterations = 4;

    public FeedbackClassifierService(
        IMicroagentHostService microagentHost,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        string? tspProjectPath = null,
        string? packagePath = null)
    {
        _microagentHost = microagentHost;
        _model = configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
        _logger = loggerFactory.CreateLogger<FeedbackClassifierService>();
        _tspProjectPath = tspProjectPath;
        _packagePath = packagePath;
    }

    /// <summary>
    /// Classifies multiple feedback items based on global context, updating their Status properties.
    /// </summary>
    /// <param name="customizableItems">List of feedback items to classify</param>
    /// <param name="globalContext">Global context containing all changes and history</param>
    /// <param name="language">Target SDK language (e.g., python, java, csharp)</param>
    /// <param name="serviceName">Service name for context (optional)</param>
    /// <param name="codeCustomizationDocUrl">URL to code customization documentation (optional)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if all items have been classified (moved to SUCCESS or FAILURE), false otherwise</returns>
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
            return true; // No items to classify
        }

        // Classify each item individually
        foreach (var item in customizableItems.ToList())
        {
            await ClassifySingleItemAsync(item, globalContext, language, serviceName, codeCustomizationDocUrl, ct);
        }

        // Check if all items are SUCCESS or FAILURE (none CUSTOMIZABLE)
        var allDone = customizableItems.All(i => i.Status != FeedbackStatus.CUSTOMIZABLE);
        return allDone;
    }

    /// <summary>
    /// Classifies a single feedback item
    /// </summary>
    private async Task ClassifySingleItemAsync(
        FeedbackItem item,
        string globalContext,
        string? language,
        string? serviceName,
        string? codeCustomizationDocUrl,
        CancellationToken ct)
    {
        // Build the classification request for single item
        var requestText = $@"**Item to Classify:**
{item.FormattedPrompt}

{globalContext}";

        // Create classification template
        var template = new FeedbackClassificationTemplate(
            serviceName: serviceName,
            language: language,
            request: requestText,
            packagePath: _packagePath,
            codeCustomizationDocUrl: codeCustomizationDocUrl
        );

        var prompt = template.BuildPrompt();

        try
        {
            // Use package path as base directory (assumes TypeSpec project is in same repo)
            var baseDir = _packagePath ?? Directory.GetCurrentDirectory();

            // Create tools for the microagent
            var tools = new List<IAgentTool>
            {
                new ReadFileTool(baseDir),
                new ListFilesTool(baseDir),
                new GrepSearchTool(baseDir),
                new FetchWebpageTool()
            };

            var result = await _microagentHost.RunAgentToCompletion(new Microagent<string>
            {
                Instructions = prompt,
                Model = _model,
                MaxToolCalls = 50,
                Tools = tools
            }, ct);

            // Log the LLM result for inspection
            _logger.LogInformation("=== LLM Classification Result (Item {ItemId}) ===", item.Id);
            _logger.LogInformation("{Result}", result);
            _logger.LogInformation("=== End LLM Result ===");

            // Parse the result and update the single item
            ParseAndApplyClassification(item, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify item {ItemId}", item.Id);
            // Mark item as FAILURE on exception
            item.Status = FeedbackStatus.FAILURE;
            item.Context += $"\nClassification failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Parses the LLM classification result and updates a single feedback item.
    /// Expected format:
    /// Classification: [PHASE_A | SUCCESS | FAILURE]
    /// Reason: <explanation>
    /// Next Action: <guidance>
    /// </summary>
    private void ParseAndApplyClassification(FeedbackItem item, string result)
    {
        // For now, assume single item classification (common case)
        // TODO: Handle multiple items in result by splitting on "---" or detecting multiple Classification: headers
        
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? classification = null;
        string? reason = null;
        var nextActionLines = new List<string>();
        bool inNextAction = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("Classification:", StringComparison.OrdinalIgnoreCase))
            {
                classification = trimmed.Substring("Classification:".Length).Trim();
            }
            else if (trimmed.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
            {
                reason = trimmed.Substring("Reason:".Length).Trim();
            }
            else if (trimmed.StartsWith("Next Action:", StringComparison.OrdinalIgnoreCase))
            {
                inNextAction = true;
                var actionText = trimmed.Substring("Next Action:".Length).Trim();
                if (!string.IsNullOrEmpty(actionText))
                {
                    nextActionLines.Add(actionText);
                }
            }
            else if (inNextAction && !string.IsNullOrWhiteSpace(trimmed))
            {
                // Continue collecting Next Action lines
                nextActionLines.Add(trimmed);
            }
        }

        if (string.IsNullOrEmpty(classification))
        {
            _logger.LogWarning("Failed to parse classification from LLM result. Defaulting to CUSTOMIZABLE.");
            return;
        }

        // Map classification to status
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
                 classification.Contains("CUSTOMIZABLE", StringComparison.OrdinalIgnoreCase))
        {
            status = FeedbackStatus.CUSTOMIZABLE;
        }
        else
        {
            _logger.LogWarning("Unknown classification: {Classification}. Defaulting to CUSTOMIZABLE.", classification);
            status = FeedbackStatus.CUSTOMIZABLE;
        }

        // Apply classification to the single item
        item.Status = status;
        
        // Store reason in property
        if (!string.IsNullOrEmpty(reason))
        {
            item.Reason = reason;
            item.Context += $"\n\nClassification: {classification}\nReason: {reason}";
        }
        
        // Set next action
        if (nextActionLines.Count > 0)
        {
            item.NextAction = string.Join("\n", nextActionLines);
        }
        
        _logger.LogInformation("Item {Id} classified as {Status}", item.Id, status);
    }
}
