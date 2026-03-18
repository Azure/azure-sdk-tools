using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators
{
    public class VerifyResultWithAIValidate : IValidator
    {
        public string Name { get; }

        public string VerificationPrompt { get; } = string.Empty;
        public VerifyResultWithAIValidate(string name, string verificationPlan)
        {
            Name = name;
            this.VerificationPrompt = "please verify the following plan: " + verificationPlan + "\nprovide the verification result. If all the verification steps are correct, respond with 'Verification successful'. Otherwise, respond with 'Verification failed'";
        }

        public async Task<ValidationResult> ValidateAsync(ValidationContext context, CancellationToken cancellationToken = default)
        {
            CopilotClient client = new CopilotClient();
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

            await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);

            SessionConfigHelper.ConfigureAgentActivityLogging(session);
            // Send prompt and wait for completion
            var messageOptions = new MessageOptions { Prompt = VerificationPrompt };

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
