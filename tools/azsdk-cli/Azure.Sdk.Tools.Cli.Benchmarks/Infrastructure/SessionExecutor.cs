// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using GitHub.Copilot.SDK;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Interaction;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Executes benchmark scenarios using the GitHub Copilot SDK.
/// </summary>
public class SessionExecutor : IDisposable
{
    private CopilotClient? _client;
    private SyntheticAICustomer? _aiCustomer;

    /// <summary>
    /// Executes a benchmark scenario with the provided configuration.
    /// </summary>
    /// <param name="config">The execution configuration.</param>
    /// <returns>The result of the execution including timing and tool call information.</returns>
    public async Task<ExecutionResult> ExecuteAsync(ExecutionConfig config)
    {
        var stopwatch = Stopwatch.StartNew();
        bool mainTaskCompleted = false;
        var toolCalls = new List<ToolCallRecord>();
        var inputQAs = new List<QuestionAndAnswer>();
        if (config.QuestionAndAnswers != null)
        {
            _aiCustomer = new SyntheticAICustomer(config.QuestionAndAnswers);
        }
        var pendingTimestamps = new Dictionary<string, double>();
        var tokenUsage = new TokenUsage();

        try
        {
            if (_client != null)
            {
                throw new InvalidOperationException("ExecuteAsync can only be called once per SessionExecutor instance. Create a new SessionExecutor for each execution.");
            }
            _client = new CopilotClient();

            // Build MCP server config - try explicit path first, then load from workspace
            var mcpServers = BuildMcpServers(config.AzsdkMcpPath)
                ?? await McpConfigLoader.LoadFromWorkspaceAsync(config.WorkingDirectory);

            var sessionConfig = new SessionConfig
            {
                WorkingDirectory = config.WorkingDirectory,
                McpServers = mcpServers,
                Model = config.Model,
                Streaming = true,
                // Auto-approve all permission requests (file edits, creates, etc.)
                OnPermissionRequest = (request, invocation) =>
                {
                    return Task.FromResult(new PermissionRequestResult
                    {
                        Kind = "approved"
                    });
                },
                Hooks = new SessionHooks
                {
                    OnPreToolUse = (input, invocation) =>
                    {
                        Console.WriteLine($"Model is calling tool: {input.ToolName}");
                        config.OnActivity?.Invoke($"Calling tool: {input.ToolName}");
                        pendingTimestamps[input.ToolName] = input.Timestamp;
                        return Task.FromResult<PreToolUseHookOutput?>(null);
                    },
                    OnPostToolUse = (input, invocation) =>
                    {
                        double? durationMs = pendingTimestamps.TryGetValue(input.ToolName, out var startTs)
                            ? input.Timestamp - startTs
                            : null;

                        var mcpServerName = input.ToolName.Contains("__")
                            ? input.ToolName.Split("__", 2)[0]
                            : null;

                        toolCalls.Add(new ToolCallRecord
                        {
                            ToolName = input.ToolName,
                            ToolArgs = input.ToolArgs,
                            ToolResult = input.ToolResult,
                            DurationMs = durationMs,
                            McpServerName = mcpServerName,
                        });

                        return Task.FromResult<PostToolUseHookOutput?>(null);
                    }
                },
                // Auto-respond to ask_user with a simple response
                OnUserInputRequest = async (request, invocation) =>
                {
                    Console.WriteLine($"Model requested user input with prompt: {request.Question}");
                    if (mainTaskCompleted)
                    {
                        Console.WriteLine("Main task already completed, This is follow-up task.");
                        return new UserInputResponse
                        {
                            Answer = "No further actions are required. End the task.",
                            WasFreeform = false
                        };
                    }

                    string answer = "Please proceed with your best judgment.";
                    if (_aiCustomer != null)
                    {
                        var result = await _aiCustomer.AskQuestionAsync(request.Question);
                        if (!string.IsNullOrEmpty(result))
                        {
                            answer = result;
                        }
                    }

                    inputQAs.Add(new QuestionAndAnswer(request.Question, answer));
                    return new UserInputResponse
                    {
                        Answer = answer,
                        WasFreeform = true
                    };
                }
            };

            await using var session = await _client.CreateSessionAsync(sessionConfig);

            // Track token usage from AssistantUsageEvent
            session.On(evt =>
            {
                if (evt is AssistantUsageEvent usageEvent)
                {
                    tokenUsage.InputTokens += usageEvent.Data.InputTokens ?? 0;
                    tokenUsage.OutputTokens += usageEvent.Data.OutputTokens ?? 0;
                    tokenUsage.CacheReadTokens += usageEvent.Data.CacheReadTokens ?? 0;
                    tokenUsage.CacheWriteTokens += usageEvent.Data.CacheWriteTokens ?? 0;
                }
                if (evt is SessionIdleEvent)
                {
                    mainTaskCompleted = true;
                }
            });
            if (config.Verbose)
            {
                SessionConfigHelper.ConfigureAgentActivityLogging(session);
            }

            // Send prompt and wait for completion
            var messageOptions = new MessageOptions { Prompt = config.Prompt };
            
            await session.SendAndWaitAsync(messageOptions, config.Timeout);

            // Get messages for debugging
            var messages = await session.GetMessagesAsync();
            // stream version

            stopwatch.Stop();
            return new ExecutionResult
            {
                Completed = true,
                Duration = stopwatch.Elapsed,
                Messages = messages.Cast<object>().ToList(),
                ToolCalls = toolCalls,
                InputQuestionAndAnswers = inputQAs,
                TokenUsage = tokenUsage
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ExecutionResult
            {
                Completed = false,
                Error = ex.Message,
                Duration = stopwatch.Elapsed,
                ToolCalls = toolCalls,
                InputQuestionAndAnswers = inputQAs,
                TokenUsage = tokenUsage
            };
        }
    }

    /// <summary>
    /// Builds MCP server configuration for the azsdk MCP server.
    /// </summary>
    /// <param name="azsdkPath">Optional path to the azsdk MCP server executable.</param>
    /// <returns>MCP server configuration dictionary, or null if no path is available.</returns>
    private static Dictionary<string, object>? BuildMcpServers(string? azsdkPath)
    {
        // Priority: config param > env var > null (let SDK use repo config)
        var path = azsdkPath ?? Environment.GetEnvironmentVariable("AZSDK_MCP_PATH");
        if (path == null)
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            ["azsdk"] = new McpLocalServerConfig
            {
                Type = "local",
                Command = path,
                Args = ["mcp"],
                Tools = ["*"],
                Env = new Dictionary<string, string>
                {
                    // Set any necessary environment variables for the MCP server here
                    // For example: ["AZURE_SDK_KB_ENDPOINT"] = "http://localhost:8088"
                    ["AZURE_SDK_KB_ENDPOINT"] = BenchmarkDefaults.DefaultAzureKnowledgeBaseEndpoint
                }
            }
        };
    }

    /// <summary>
    /// Disposes of the Copilot client and releases resources.
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
