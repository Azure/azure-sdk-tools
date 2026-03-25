// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using GitHub.Copilot.SDK;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Interaction
{
    public class SyntheticAICustomer
    {
        public IReadOnlyList<QuestionAndAnswer> QuestionsAndAnswers { get; }
        public SyntheticAICustomer(IReadOnlyList<QuestionAndAnswer> questionsAndAnswers)
        {
            QuestionsAndAnswers = questionsAndAnswers;
        }

        public async Task<string> AskQuestionAsync(string question)
        {
            using var client = new CopilotClient();
            var sessionConfig = new SessionConfig
            {
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

            SessionConfigHelper.ConfigureAgentActivityLogging(session);
            var messageOptions = new MessageOptions { Prompt = BuildPrompt(question) };

            var result = await session.SendAndWaitAsync(messageOptions, TimeSpan.FromMinutes(5));
            if (result == null || result.Data == null || string.IsNullOrEmpty(result.Data.Content))
            {
                return "";
            }
            return result.Data.Content;
        }
        private string BuildPrompt(string question)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SYSTEM INSTRUCTION:");
            sb.AppendLine("You are a synthetic customer in a benchmark test. Please answer the questions asked by the agent based on the context provided. If you cannot find out answer, please reply empty string.");
            sb.AppendLine("Here are some questions and answers for context:");
            foreach (var qa in QuestionsAndAnswers)
            {
                sb.AppendLine($"Q: {qa.Question}");
                sb.AppendLine($"A: {qa.Answer}");
                sb.AppendLine();
            }
            sb.AppendLine("");
            sb.AppendLine("Please answer the following question:");
            sb.AppendLine(question);
            return sb.ToString();
        }
    }

    public record QuestionAndAnswer(string Question, string Answer);
}
