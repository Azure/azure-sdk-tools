// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

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
public class CopilotTokenUsageHelper(IRawOutputHelper outputHelper) : TokenUsageHelper(outputHelper)
{
    // Previous cumulative input tokens (for delta calculation)
    private double previousCumulativeInputTokens = 0;

    // Previous cumulative cache read tokens (for delta calculation)
    private double previousCumulativeCacheReadTokens = 0;

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
        ModelsUsed = ModelsUsed.Union([model]);

        // inputTokens is cumulative, so compute the delta from previous turn
        var inputTokensDelta = inputTokens - previousCumulativeInputTokens;
        var cacheReadTokensDelta = cacheReadTokens - previousCumulativeCacheReadTokens;

        // Actual processed input this turn = new tokens minus those read from cache
        var processedInputThisTurn = inputTokensDelta - cacheReadTokensDelta;

        // Update previous cumulative values for next turn
        previousCumulativeInputTokens = inputTokens;
        previousCumulativeCacheReadTokens = cacheReadTokens;

        // Sum incremental values
        CompletionTokens += outputTokens;
        PromptTokens += processedInputThisTurn;

        UpdateTelemetry();
    }
}
