// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Helpers;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// Tracks token usage for Copilot SDK agents.
/// 
/// The Copilot SDK reports inputTokens as the cumulative context size (not incremental).
/// Each turn's inputTokens = system prompt + all previous messages + current turn.
/// outputTokens IS incremental per turn, so we sum those.
/// cacheReadTokens shows how much of inputTokens came from cache (not reprocessed).
/// 
/// To avoid overcounting, we track the previous cumulative inputTokens and compute deltas.
/// </summary>
public class CopilotTokenUsageHelper(IRawOutputHelper outputHelper)
{
    // Sum of output tokens (these ARE incremental per turn)
    private double totalOutputTokens = 0;

    // Previous cumulative input tokens (for delta calculation)
    private double previousCumulativeInputTokens = 0;

    // Previous cumulative cache read tokens (for delta calculation)
    private double previousCumulativeCacheReadTokens = 0;

    // Count API calls (turns)
    private int turnCount = 0;

    // Models used
    private IEnumerable<string> modelsUsed = [];

    /// <summary>
    /// Total processed input tokens (actual tokens sent, excluding cache hits).
    /// </summary>
    public double PromptTokens { get; private set; } = 0;

    /// <summary>
    /// Total output tokens generated.
    /// </summary>
    public double CompletionTokens => totalOutputTokens;

    /// <summary>
    /// Total billable tokens (processed input + output).
    /// </summary>
    public double TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Records token usage for a single turn/API call.
    /// </summary>
    /// <param name="model">The model used for this turn.</param>
    /// <param name="inputTokens">Cumulative context size (not incremental).</param>
    /// <param name="outputTokens">Output tokens for this turn (incremental).</param>
    /// <param name="cacheReadTokens">Tokens read from cache this turn.</param>
    /// <param name="cacheWriteTokens">Tokens written to cache this turn.</param>
    public void Add(string model, double inputTokens, double outputTokens, double cacheReadTokens = 0, double cacheWriteTokens = 0)
    {
        turnCount++;
        modelsUsed = modelsUsed.Union([model]);

        // inputTokens is cumulative, so compute the delta from previous turn
        var inputTokensDelta = inputTokens - previousCumulativeInputTokens;
        var cacheReadTokensDelta = cacheReadTokens - previousCumulativeCacheReadTokens;

        // Actual processed input this turn = new tokens minus those read from cache
        var processedInputThisTurn = inputTokensDelta - cacheReadTokensDelta;

        // Update previous cumulative values for next turn
        previousCumulativeInputTokens = inputTokens;
        previousCumulativeCacheReadTokens = cacheReadTokens;

        // Sum incremental values
        totalOutputTokens += outputTokens;
        PromptTokens += processedInputThisTurn;

        Activity.Current?.SetCustomProperty(TagName.PromptTokens, PromptTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.CompletionTokens, CompletionTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.TotalTokens, TotalTokens.ToString("F0"));
        Activity.Current?.SetCustomProperty(TagName.ModelsUsed, string.Join(",", modelsUsed.OrderBy(m => m)));
    }

    /// <summary>
    /// Logs a summary of token usage to the console.
    /// </summary>
    public void LogUsage()
    {
        var models = string.Join(", ", modelsUsed);

        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
        outputHelper.OutputConsole($"[token usage][{models}] input: {PromptTokens}, output: {CompletionTokens}, total: {TotalTokens}");
        outputHelper.OutputConsole("--------------------------------------------------------------------------------");
    }
}
