// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

/// <summary>
/// Debug validator that asks the LLM to report its full system context.
/// Useful for understanding what copilot instructions, skills, and files are loaded.
/// This validator always passes - it's purely for debugging/inspection.
/// </summary>
public class ContextReportValidator : IValidator
{
    /// <summary>
    /// Gets the name of this validator.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the model to use.
    /// </summary>
    public string Model { get; init; } = "claude-sonnet-4.5";

    /// <summary>
    /// Creates a new context report validator.
    /// </summary>
    /// <param name="name">Human-readable name for the validator.</param>
    public ContextReportValidator(string name = "Context Report")
    {
        Name = name;
    }

    public async Task<ValidationResult> ValidateAsync(
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Run in the scenario's workspace directory to pick up any copilot config
            using var client = new CopilotClient(new CopilotClientOptions
            {
                Cwd = context.RepoPath
            });

            var sessionConfig = new SessionConfig
            {
                Model = Model,
                Streaming = false,
                // DON'T replace system message - we want to see what's loaded
                // AvailableTools = [] would disable tools, but we want to see what's available
                InfiniteSessions = new InfiniteSessionConfig { Enabled = false }
            };

            await using var session = await client.CreateSessionAsync(sessionConfig);

            var prompt = """
                Please provide a complete report of your current context:

                1. **System Prompt**: Summarize the key instructions in your system prompt. What are you being told to do or not do? Any special rules?

                2. **Copilot Instructions**: Are there any copilot instructions loaded (from .github/copilot-instructions.md or similar)? If so, summarize them.

                3. **Skills**: List any skills that are available to you.

                4. **MCP Servers/Tools**: List any MCP servers or custom tools that are configured.

                5. **Files in Context**: List any files that have been pre-loaded into your context.

                6. **Working Directory**: What is your current working directory?

                Be thorough - this is for debugging purposes.
                """;

            var messageOptions = new MessageOptions { Prompt = prompt };
            await session.SendAndWaitAsync(messageOptions, TimeSpan.FromMinutes(2));

            // Get the response
            var messages = await session.GetMessagesAsync();
            var lastAssistantMessage = messages
                .OfType<AssistantMessageEvent>()
                .LastOrDefault();

            var responseContent = lastAssistantMessage?.Data?.Content ?? "(No response)";

            stopwatch.Stop();

            // Always pass - this is just for inspection
            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = true,
                Message = "Context report generated (see details)",
                Details = responseContent,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationResult
            {
                ValidatorName = Name,
                Passed = true, // Still pass - don't fail the run for debug info
                Message = $"Failed to generate context report: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
    }
}
