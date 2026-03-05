// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Executes benchmark scenarios using the GitHub Copilot SDK.
/// </summary>
public class SessionExecutor : IDisposable
{
    private CopilotClient? _client;

    /// <summary>
    /// Executes a benchmark scenario with the provided configuration.
    /// </summary>
    /// <param name="config">The execution configuration.</param>
    /// <returns>The result of the execution including timing and tool call information.</returns>
    public async Task<ExecutionResult> ExecuteAsync(ExecutionConfig config)
    {
        var stopwatch = Stopwatch.StartNew();
        var toolCalls = new List<ToolCallRecord>();
        var pendingStarts = new ConcurrentStack<(long Timestamp, string? ArgsJson)>();

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
                Hooks = new SessionHooks
                {
                    OnPreToolUse = (input, invocation) =>
                    {
                        config.OnActivity?.Invoke($"Calling tool: {input.ToolName}");

                        // Capture start timestamp and arguments for duration tracking
                        string? argsJson = null;
                        try
                        {
                            if (input.ToolArgs != null)
                            {
                                argsJson = JsonSerializer.Serialize(input.ToolArgs);
                            }
                        }
                        catch { /* ignore serialization errors */ }

                        pendingStarts.Push((input.Timestamp, argsJson));

                        return Task.FromResult<PreToolUseHookOutput?>(null);
                    },
                    OnPostToolUse = (input, invocation) =>
                    {
                        double? durationMs = null;
                        string? args = null;

                        if (pendingStarts.TryPop(out var startInfo))
                        {
                            // Timestamps are in milliseconds from the SDK
                            durationMs = input.Timestamp - startInfo.Timestamp;
                            args = startInfo.ArgsJson;
                        }

                        // Try to extract MCP server name from tool name convention (server__tool)
                        string? mcpServerName = null;
                        var toolName = input.ToolName;
                        if (toolName.Contains("__"))
                        {
                            var parts = toolName.Split("__", 2);
                            mcpServerName = parts[0];
                        }

                        toolCalls.Add(new ToolCallRecord
                        {
                            ToolName = input.ToolName,
                            Arguments = args,
                            DurationMs = durationMs,
                            McpServerName = mcpServerName,
                            Timestamp = DateTime.UtcNow
                        });

                        return Task.FromResult<PostToolUseHookOutput?>(null);
                    }
                },
                // Auto-respond to ask_user with a simple response
                OnUserInputRequest = (request, invocation) =>
                {
                    return Task.FromResult(new UserInputResponse
                    {
                        Answer = "Please proceed with your best judgment.",
                        WasFreeform = true
                    });
                }
            };

            await using var session = await _client.CreateSessionAsync(sessionConfig);

            // Send prompt and wait for completion
            var messageOptions = new MessageOptions { Prompt = config.Prompt };
            await session.SendAndWaitAsync(messageOptions, config.Timeout);

            // Get messages for debugging
            var messages = await session.GetMessagesAsync();

            stopwatch.Stop();
            return new ExecutionResult
            {
                Completed = true,
                Duration = stopwatch.Elapsed,
                Messages = messages.Cast<object>().ToList(),
                ToolCalls = toolCalls
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
                ToolCalls = toolCalls
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
        if (path == null) return null;

        return new Dictionary<string, object>
        {
            ["azsdk"] = new McpLocalServerConfig
            {
                Type = "local",
                Command = path,
                Args = ["mcp", "run"],
                Tools = ["*"]
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
