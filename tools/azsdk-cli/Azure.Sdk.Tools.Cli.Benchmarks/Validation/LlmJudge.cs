// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation;

/// <summary>
/// Result of an LLM judgment.
/// </summary>
public class JudgmentResult
{
    /// <summary>
    /// Gets whether the judgment passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the reasoning provided by the LLM.
    /// </summary>
    public required string Reasoning { get; init; }
}

/// <summary>
/// Helper for making LLM judgment calls using the GitHub Copilot SDK.
/// Used by validators that need LLM-based evaluation.
/// </summary>
public class LlmJudge : IDisposable
{
    private CopilotClient? _client;

    /// <summary>
    /// Asks the LLM to make a pass/fail judgment.
    /// </summary>
    /// <param name="systemPrompt">The system prompt defining the judge's role.</param>
    /// <param name="userPrompt">The user prompt with the content to judge.</param>
    /// <param name="model">The model to use (default: claude-sonnet-4.5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The judgment result with pass/fail and reasoning.</returns>
    public async Task<JudgmentResult> JudgeAsync(
        string systemPrompt,
        string userPrompt,
        string model = "claude-sonnet-4.5",
        CancellationToken cancellationToken = default)
    {
        // Use a temp directory to avoid loading copilot-instructions.md or skills
        var tempDir = Path.Combine(Path.GetTempPath(), $"llm-judge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _client ??= new CopilotClient(new CopilotClientOptions
            {
                Cwd = tempDir
            });

            var sessionConfig = new SessionConfig
            {
                Model = model,
                Streaming = false,
                SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                },
                // Disable all tools - we just want a text response
                AvailableTools = [],
                McpServers = null,
                // Disable infinite sessions to keep context clean
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
            };

            await using var session = await _client.CreateSessionAsync(sessionConfig);

            var messageOptions = new MessageOptions { Prompt = userPrompt };
            await session.SendAndWaitAsync(messageOptions, TimeSpan.FromMinutes(2));

            // Get the last assistant message content
            var messages = await session.GetMessagesAsync();
            var lastAssistantMessage = messages
                .OfType<AssistantMessageEvent>()
                .LastOrDefault();
            
            var responseContent = lastAssistantMessage?.Data?.Content ?? "";

            // Parse the response - look for PASS or FAIL
            var passed = responseContent.Contains("PASS", StringComparison.OrdinalIgnoreCase) &&
                        !responseContent.Contains("FAIL", StringComparison.OrdinalIgnoreCase);
            
            // If response contains FAIL, it's a failure
            if (responseContent.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                passed = false;
            }

            return new JudgmentResult
            {
                Passed = passed,
                Reasoning = responseContent
            };
        }
        finally
        {
            // Cleanup temp directory
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
