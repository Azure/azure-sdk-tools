using System.ComponentModel;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.CopilotAgents;

public class CopilotAgentRunner(
    ICopilotClientWrapper client,
    CopilotTokenUsageHelper tokenUsageHelper,
    ILogger<CopilotAgentRunner> logger) : ICopilotAgentRunner
{

    public async Task<TResult> RunAsync<TResult>(
        CopilotAgent<TResult> agent,
        CancellationToken ct = default) where TResult : notnull
    {
        // Validate no tool is named "Exit" (reserved name) - case-insensitive
        if (agent.Tools.Any(t => string.Equals(t.Name, "Exit", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Cannot name a tool with the special name 'Exit'. Please choose a different name.", nameof(agent));
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

        // Collect tools + Exit tool
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

        // Create session config with tools
        var sessionConfig = new SessionConfig
        {
            Model = agent.Model,
            Tools = tools,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = agent.Instructions
            }
        };
        
        await using var session = await client.CreateSessionAsync(sessionConfig, ct);

        // TaskCompletionSource to signal when session is idle
        TaskCompletionSource? sessionIdleTcs = null;
        // Track session errors that occur during processing
        SessionErrorEvent? sessionError = null;

        // Subscribe to events for token tracking BEFORE sending messages
        // Events are dispatched during SendAsync, so handlers must be registered first
        using var eventSubscription = session.On(evt =>
        {
            logger.LogDebug("Received session event: {EventType}", evt.GetType().Name);
            switch (evt)
            {
                case AssistantUsageEvent usage:
                    tokenUsageHelper.Add(
                        usage.Data.Model ?? agent.Model,
                        usage.Data.InputTokens ?? 0,
                        usage.Data.OutputTokens ?? 0);
                    break;
                case AssistantMessageEvent msg:
                    logger.LogDebug("Assistant message: {Content}", msg.Data.Content?[..Math.Min(100, msg.Data.Content?.Length ?? 0)]);
                    break;
                case ToolExecutionStartEvent toolStart:
                    logger.LogDebug("Tool execution started: {ToolName}", toolStart.Data.ToolName);
                    break;
                case ToolExecutionCompleteEvent toolComplete:
                    logger.LogDebug("Tool execution completed: {ToolCallId} success={Success}", toolComplete.Data.ToolCallId, toolComplete.Data.Success);
                    break;
                case SessionErrorEvent error:
                    logger.LogError("Session error: {ErrorType} - {Message}", error.Data.ErrorType, error.Data.Message);
                    sessionError = error;
                    break;
                case SessionIdleEvent:
                    logger.LogDebug("Session is idle, signaling completion");
                    sessionIdleTcs?.TrySetResult();
                    break;
            }
        });

        // Validation retry loop
        var prompt = "Begin the task. Call tools as needed, then call Exit with the result. You are running autonomously and must not ask for further input.";
        var iterations = 0;
        const int maxExitRetries = 3;
        var exitRetries = 0;

        while (iterations < agent.MaxIterations)
        {
            iterations++;
            capturedResult = default;
            sessionError = null; // Reset error state for each iteration

            logger.LogDebug("Sending message iteration {Iteration}", iterations);
            
            // Create TCS before sending to ensure we don't miss the event
            sessionIdleTcs = new TaskCompletionSource();
            
            // SendAsync returns the message ID but doesn't wait for processing
            // We need to wait for SessionIdleEvent to know when the agent is done
            await session.SendAsync(new MessageOptions { Prompt = prompt }, ct);
            
            // Wait for the session to become idle (all tool calls completed)
            // Use cancellation-aware wait with timeout to prevent indefinite hangs
            logger.LogDebug("Waiting for session to become idle...");
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(agent.IdleTimeout);
            await sessionIdleTcs.Task.WaitAsync(linkedCts.Token);
            
            logger.LogDebug("Message completed, capturedResult is {HasResult}", capturedResult != null ? "set" : "null");

            // Check if there was a session error
            if (sessionError != null)
            {
                throw new InvalidOperationException(
                    $"Session error during agent execution: [{sessionError.Data.ErrorType}] {sessionError.Data.Message}");
            }

            // Check if Exit was called
            if (capturedResult == null)
            {
                exitRetries++;
                if (exitRetries >= maxExitRetries)
                {
                    throw new InvalidOperationException(
                        $"Agent failed to call Exit tool after {maxExitRetries} reminders");
                }
                logger.LogWarning("Agent completed without calling Exit tool (attempt {Attempt}/{Max}). Prompting to call Exit.", 
                    exitRetries, maxExitRetries);
                prompt = "You did not call the Exit tool. You are running autonomously and must not ask for user input or confirmation. " +
                         "If the task is incomplete, continue working. If the task is complete, call the Exit tool with your result now.";
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
                    prompt = $"The result you provided did not pass validation: {reason}. Please try again.";
                    continue;
                }
            }

            // Success
            return capturedResult;
        }

        throw new InvalidOperationException(
            $"Agent did not return a valid result within {agent.MaxIterations} iterations");
    }
}
