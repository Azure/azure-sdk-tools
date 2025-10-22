using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using ToolChatMessage = OpenAI.Chat.ToolChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;

namespace Azure.Sdk.Tools.McpEvals.Helpers
{
    public static class ChatMessageHelper
    {
        public static Dictionary<string, ChatMessage> GetExpectedToolsByName(IEnumerable<ChatMessage> expectedOutcome, IEnumerable<string> toolNames)
        {
            var expectedToolResults = new Dictionary<string, ChatMessage>();

            // Create CallId -> ToolName mapping
            // Tool Name is not available in FunctionResultContent
            // Normalize function names and remove tools used not in toolNames list
            var callIdToName = expectedOutcome
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .Select(fc => new { fc.CallId, Normalized = SerializationHelper.NormalizeFunctionName(fc.Name, toolNames) })
                .Where(x => !string.IsNullOrEmpty(x.Normalized))
                .ToDictionary(x => x.CallId, x => x.Normalized);

            foreach (var message in expectedOutcome)
            {
                foreach (var content in message.Contents)
                {
                    if (content is FunctionResultContent funcResult && callIdToName.TryGetValue(funcResult.CallId, out var functionName))
                    {
                        expectedToolResults[functionName] = message;
                    }
                }
            }

            return expectedToolResults;
        }

        public static List<ChatMessage> EnsureChatMessageRole(IEnumerable<ChatMessage> translatedMessages, IEnumerable<OpenAIChatMessage> openAIMessages)
        {
            var result = new List<ChatMessage>();

            foreach (var (translated, openAI) in translatedMessages.Zip(openAIMessages))
            {
                // Create a copy or modify the existing one
                var modifiedMessage = translated; // or create a copy if you don't want to modify the original

                switch (openAI)
                {
                    case AssistantChatMessage:
                        modifiedMessage.Role = ChatRole.Assistant;
                        break;
                    case UserChatMessage:
                        modifiedMessage.Role = ChatRole.User;
                        break;
                    case SystemChatMessage:
                        modifiedMessage.Role = ChatRole.System;
                        break;
                    case ToolChatMessage:
                        modifiedMessage.Role = ChatRole.Tool;
                        break;
                }

                result.Add(modifiedMessage);
            }

            return result;
        }

        public static ScenarioData ParseChatMessagesIntoScenarioData(List<ChatMessage> chatMessages)
        {
            // Use List<T>.FindLastIndex for clear intent and minimal code.
            int lastUserMessageIndex = chatMessages.FindLastIndex(m => m.Role == ChatRole.User);
            if (lastUserMessageIndex < 0)
            {
                throw new InvalidOperationException("No user message found in the chat messages list");
            }

            return new ScenarioData
            {
                ChatHistory = chatMessages.Take(lastUserMessageIndex).ToList(),
                NextMessage = chatMessages[lastUserMessageIndex],
                ExpectedOutcome = chatMessages.Skip(lastUserMessageIndex + 1).ToList()
            };
        }

        public static ScenarioData LoadScenarioFromPrompt(string prompt, IEnumerable<string> tools)
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, LLMSystemInstructions.BuildLLMInstructions())
            };
            var nextMessage = new ChatMessage(ChatRole.User, prompt);
            var toolCalls = ToolMocks.ToolMocks.GetToolMocks(tools);

            var expectedOutcome = toolCalls.SelectMany(t => t.GetMockCallAndResponse());

            return new ScenarioData
            {
                ChatHistory = history,
                NextMessage = nextMessage,
                ExpectedOutcome = expectedOutcome
            };
        }
    }
}
