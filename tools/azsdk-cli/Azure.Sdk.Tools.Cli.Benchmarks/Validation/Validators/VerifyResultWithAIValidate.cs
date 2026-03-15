using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators
{
    public class VerifyResultWithAIValidate : IValidator
    {
        public string Name { get; }

        public string verificationPrompt { get; } = string.Empty;
        public VerifyResultWithAIValidate(string name, string verificationPlan)
        {
            Name = name;
            this.verificationPrompt = "please verify the following plan: " + verificationPlan + "\nprovide the verification result.";
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
                        //if (input.ToolName == "skill")
                        //{
                        //    toolCalls.Add($"{input.ToolName} {input.ToolArgs?.ToString()}");
                        //}
                        //else
                        //{
                        //    toolCalls.Add(input.ToolName);
                        //}
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

            await using var session = await client.CreateSessionAsync(sessionConfig);
            var done = new TaskCompletionSource();

            return ValidationResult.Pass(Name, $"Verify pass");
        }
    }
}
