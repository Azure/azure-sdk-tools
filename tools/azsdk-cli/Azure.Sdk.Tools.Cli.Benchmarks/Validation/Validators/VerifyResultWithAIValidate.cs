// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators
{
    public class VerifyResultWithAIValidator : IValidator
    {
        public string Name { get; }

        public string VerificationPrompt { get; } = string.Empty;
        public VerifyResultWithAIValidator(string name, string verificationPlan)
        {
            Name = name;
            this.VerificationPrompt = "please verify the following plan: " + verificationPlan + "\nprovide the verification result. If all the verification steps are correct, respond with 'Verification successful'. Otherwise, respond with 'Verification failed'";
        }

        public async Task<ValidationResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
        {
            using CopilotClient client = new CopilotClient();
            var sessionConfig = new SessionConfig
            {
                WorkingDirectory = context.RepoPath,
                Streaming = true,
                Model = BenchmarkDefaults.DefaultModel,
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
                        return Task.FromResult<PreToolUseHookOutput?>(null);
                    },
                    OnPostToolUse = (input, invocation) =>
                    {
                        return Task.FromResult<PostToolUseHookOutput?>(null);
                    }
                },
                // Auto-respond to ask_user with a simple response
                OnUserInputRequest = (request, invocation) =>
                {
                    Console.WriteLine($"Model requested user input with prompt: {request.Question}");
                    return Task.FromResult(new UserInputResponse
                    {
                        Answer = "Please proceed with your best judgment.",
                        WasFreeform = true
                    });
                }
            };

            // Log the Interaction question and answers if available for late failure analysis
            if (context.InputQuestionAndAnswers != null && context.InputQuestionAndAnswers.Count > 0)
            {
                Console.WriteLine("The Interaction question and answers:" + string.Join(", ", context.InputQuestionAndAnswers.Select(qa => $"{qa.Question}: {qa.Answer}")));
            } else
            {
                Console.WriteLine("No Interaction question and answers during execution.");
            }

            await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);

            SessionConfigHelper.ConfigureAgentActivityLogging(session);
            // Send prompt and wait for completion
            var gitDiff = await context.Workspace.GetGitDiffAsync();

            var fullPrompt = VerificationPrompt;
            if (!string.IsNullOrEmpty(gitDiff))
            {
                fullPrompt += "\n\n## Typespec Git Diff:\n" + gitDiff;
            }

            var messageOptions = new MessageOptions { Prompt = fullPrompt };

            var result = await session.SendAndWaitAsync(messageOptions, TimeSpan.FromMinutes(5), cancellationToken);

            if (result == null || result.Data == null || string.IsNullOrEmpty(result.Data.Content))
            {
                return ValidationResult.Fail(
                    Name,
                    "AI verification did not return a result within the expected time or returned empty content.");
            }

            if (result.Data.Content.Contains("Verification successful", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Pass(Name, $"AI verification passed");
            }
            else
            {
                return ValidationResult.Fail(Name, $"AI verification failed", result.Data.Content);
            }
        }
    }
}
