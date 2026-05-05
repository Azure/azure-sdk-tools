// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

/// <summary>
/// An <see cref="ICopilotAgentRunner"/> implementation that uses MCP sampling instead of the GitHub Copilot SDK.
///
/// When this MCP server is called by an MCP client that supports sampling, the server can
/// delegate LLM inference back to the client via <c>server.AsSamplingChatClient()</c>.
/// This removes the dependency on the Copilot CLI for LLM calls, using the standard MCP
/// sampling protocol instead.
///
/// The runner implements the same agent loop as <see cref="CopilotAgentRunner"/>:
///   1. Build a message list with system prompt + user message
///   2. Call the LLM (via sampling) with available tools
///   3. Execute any tool calls the LLM requests locally
///   4. Repeat until the LLM calls the special "Exit" tool
///   5. Validate the result and retry if needed
/// </summary>
public class SamplingAgentRunner(
    IMcpServerContextAccessor mcpServerContextAccessor,
    TokenUsageHelper tokenUsageHelper,
    ILogger<SamplingAgentRunner> logger) : ICopilotAgentRunner
{
    public async Task<TResult> RunAsync<TResult>(
        CopilotAgent<TResult> agent,
        CancellationToken ct = default) where TResult : notnull
    {
        var server = mcpServerContextAccessor.Current
            ?? throw new InvalidOperationException(
                "MCP sampling requires an active MCP server context. " +
                "This runner can only be used when the tool is invoked via an MCP client that supports sampling.");

        // Validate no tool is named "Exit" (reserved name) - case-insensitive
        if (agent.Tools.Any(t => string.Equals(t.Name, "Exit", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Cannot name a tool with the special name 'Exit'. Please choose a different name.",
                nameof(agent));
        }

        // Validate no duplicate tool names (case-insensitive)
        var duplicateNames = agent.Tools
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new ArgumentException(
                $"Duplicate tool names detected: {string.Join(", ", duplicateNames)}. Each tool must have a unique name.",
                nameof(agent));
        }

        // Build tools list including Exit tool
        var tools = agent.Tools.ToList();
        TResult? capturedResult = default;

        tools.Add(AIFunctionFactory.Create(
            ([Description("The result of the agent run. Output the result requested exactly, without additional padding, explanation, or code fences unless requested.")]
             TResult result) =>
            {
                capturedResult = result;
                return "Exiting with result";
            },
            "Exit",
            "Call this tool when you are finished with the work or are otherwise unable to continue."));

        // Build a lookup for executing tool calls locally
        var toolLookup = tools.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        // Get an IChatClient from the MCP server's sampling capability.
        // This delegates LLM calls back to the MCP client (e.g. Copilot, Claude Desktop, etc.)
        // Note: AsSamplingChatClient() only handles the LLM call — tool execution is done locally.
        var samplingClient = server.AsSamplingChatClient();

        // Build the initial conversation
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, agent.Instructions),
            new(ChatRole.User,
                "Begin the task. Call tools as needed, then call Exit with the result. " +
                "You are running autonomously and must not ask for further input.")
        };

        var chatOptions = new ChatOptions
        {
            Tools = [.. tools],
            ToolMode = ChatToolMode.Auto
        };

        var iterations = 0;
        const int maxExitRetries = 3;
        var exitRetries = 0;

        while (iterations < agent.MaxIterations)
        {
            iterations++;
            capturedResult = default;

            logger.LogDebug("Sampling iteration {Iteration}", iterations);

            // Inner loop: keep calling the LLM and executing tool calls until
            // the LLM produces a final text response (no more tool calls).
            var turnComplete = false;
            while (!turnComplete)
            {
                // Enforce per-turn timeout matching CopilotAgentRunner behavior
                using var timeoutCts = new CancellationTokenSource(agent.IdleTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                ChatResponse response;
                try
                {
                    response = await samplingClient.GetResponseAsync(messages, chatOptions, linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Agent session idle timeout of {agent.IdleTimeout.TotalMinutes}m was exceeded while waiting for sampling response.");
                }

                // Track token usage if available
                TrackUsage(response, agent.Model);

                // Add the assistant response to conversation history
                messages.AddRange(response.Messages);

                // Check for tool calls in the response
                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (toolCalls.Count == 0)
                {
                    // No tool calls — the LLM has finished this turn
                    turnComplete = true;
                    break;
                }

                // Execute each tool call locally and add results to conversation
                var toolResults = new List<AIContent>();
                foreach (var toolCall in toolCalls)
                {
                    logger.LogDebug("Executing tool: {ToolName} (call: {CallId})", toolCall.Name, toolCall.CallId);

                    if (!toolLookup.TryGetValue(toolCall.Name, out var tool))
                    {
                        logger.LogWarning("LLM requested unknown tool: {ToolName}", toolCall.Name);
                        toolResults.Add(new FunctionResultContent(
                            toolCall.CallId,
                            $"Error: Unknown tool '{toolCall.Name}'"));
                        continue;
                    }

                    try
                    {
                        var args = toolCall.Arguments != null
                            ? new AIFunctionArguments(toolCall.Arguments)
                            : null;
                        var result = await tool.InvokeAsync(args, ct);
                        var resultStr = result?.ToString() ?? string.Empty;
                        logger.LogDebug("Tool {ToolName} completed successfully", toolCall.Name);
                        toolResults.Add(new FunctionResultContent(
                            toolCall.CallId,
                            resultStr));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Tool {ToolName} failed", toolCall.Name);
                        toolResults.Add(new FunctionResultContent(
                            toolCall.CallId,
                            $"Error executing tool: {ex.Message}"));
                    }
                }

                messages.Add(new ChatMessage(ChatRole.Tool, toolResults));

                // If Exit was called during tool execution, break out of the inner loop
                if (capturedResult != null)
                {
                    turnComplete = true;
                }
            }

            logger.LogDebug("Turn completed, capturedResult is {HasResult}",
                capturedResult != null ? "set" : "null");

            // Check if Exit was called
            if (capturedResult == null)
            {
                exitRetries++;
                if (exitRetries >= maxExitRetries)
                {
                    throw new InvalidOperationException(
                        $"Agent failed to call Exit tool after {maxExitRetries} reminders");
                }
                logger.LogWarning(
                    "Agent completed without calling Exit tool (attempt {Attempt}/{Max}). Prompting to call Exit.",
                    exitRetries, maxExitRetries);

                messages.Add(new ChatMessage(ChatRole.User,
                    "You did not call the Exit tool. You are running autonomously and must not ask for user input or confirmation. " +
                    "If the task is incomplete, continue working. If the task is complete, call the Exit tool with your result now."));
                continue;
            }

            // Reset exit retries on successful Exit call
            exitRetries = 0;

            // Validate result if validator provided
            if (agent.ValidateResult != null)
            {
                var validation = await agent.ValidateResult(capturedResult);
                if (!validation.Success)
                {
                    var reason = validation.Reason is string str
                        ? str
                        : JsonSerializer.Serialize(validation.Reason);
                    logger.LogWarning("Agent result failed validation: {Reason}. Retrying.", reason);

                    messages.Add(new ChatMessage(ChatRole.User,
                        $"The result you provided did not pass validation: {reason}. Try again."));
                    continue;
                }
            }

            // Success
            tokenUsageHelper.LogUsage();
            return capturedResult;
        }

        tokenUsageHelper.LogUsage();
        throw new InvalidOperationException(
            $"Agent did not return a valid result within {agent.MaxIterations} iterations");
    }

    /// <summary>
    /// Extracts token usage from the ChatResponse if available.
    /// Note: MCP sampling may not always provide usage data — this is best-effort.
    /// </summary>
    private void TrackUsage(ChatResponse response, string model)
    {
        var usage = response.Usage;
        if (usage != null)
        {
            var inputTokens = usage.InputTokenCount ?? 0;
            var outputTokens = usage.OutputTokenCount ?? 0;
            if (inputTokens > 0 || outputTokens > 0)
            {
                var responseModel = response.ModelId ?? model;
                tokenUsageHelper.Add(responseModel, inputTokens, outputTokens);
                logger.LogDebug("Token usage - model: {Model}, input: {Input}, output: {Output}",
                    responseModel, inputTokens, outputTokens);
            }
        }
    }
}
