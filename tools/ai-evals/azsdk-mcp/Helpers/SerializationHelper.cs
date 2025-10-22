using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI;
using MicrosoftExtensionsAIChatExtensions = OpenAI.Chat.MicrosoftExtensionsAIChatExtensions;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using AssistantChatMessage = OpenAI.Chat.AssistantChatMessage;
using UserChatMessage = OpenAI.Chat.UserChatMessage;
using SystemChatMessage = OpenAI.Chat.SystemChatMessage;
using ToolChatMessage = OpenAI.Chat.ToolChatMessage;

namespace Azure.Sdk.Tools.McpEvals.Helpers
{
    public static class SerializationHelper
    {

        /// <summary>
        /// Loads scenario data from a JSON file containing a list of chat messages.
        /// The last user message becomes the NextMessage, everything before it becomes ChatHistory,
        /// and everything after it becomes ExpectedOutcome.
        /// </summary>
        /// <param name="jsonPath">Path to the JSON file containing an array of chat messages</param>
        /// <returns>ScenarioData with parsed chat messages</returns>
        public static async Task<ScenarioData> LoadScenarioFromChatMessagesAsync(string jsonPath)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath);

            var jsonBinary = BinaryData.FromString(jsonContent);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var chatMessages = DeserializeMessages(jsonBinary);

            var translatedChatMessages = MicrosoftExtensionsAIChatExtensions.AsChatMessages(chatMessages);

            // For some reason above method will give all the messages user role. It still does the heavy lifting so I'll keep it and just change the roles. 
            var fixedChatMessages = EnsureChatMessageRole(translatedChatMessages, chatMessages);

            await RebuildAttachmentsInChatMessagesAsync(fixedChatMessages);

            return ParseChatMessagesIntoScenarioData(fixedChatMessages);
        }

        public static ScenarioData LoadScenarioFromPrompt(string prompt, IEnumerable<string> tools)
        {
            var history = new List<ChatMessage>
            {
                new(ChatRole.System, LLMSystemInstructions.BuildLLMInstructions())
            };
            var nextMessage = new ChatMessage(ChatRole.User, prompt);
            var expectedOutcome = new List<ChatMessage>();
            return new ScenarioData
            {
                ChatHistory = history,
                NextMessage = nextMessage,
                ExpectedOutcome = expectedOutcome
            };
        }

        /// <summary>
        /// Rebuilds attachments in the first chat message by replacing attachment content
        /// with files from the configured instruction directories.
        /// </summary>
        /// <param name="chatMessages">The chat messages to process</param>
        public static async Task RebuildAttachmentsInChatMessagesAsync(List<ChatMessage> chatMessages)
        {
            // Only process the first message (typically the system message)
            if (chatMessages.Count == 0)
                return;

            var firstMessage = chatMessages[0];
            if (firstMessage.Contents[0] is TextContent textContent && !string.IsNullOrEmpty(textContent.Text))
            {
                textContent.Text = await RebuildAttachmentsInTextAsync(textContent.Text);
            }
        }

        private static ScenarioData ParseChatMessagesIntoScenarioData(List<ChatMessage> chatMessages)
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

        /// <summary>
        /// Rebuilds attachment sections in text content by loading files from instruction directories.
        /// </summary>
        private static async Task<string> RebuildAttachmentsInTextAsync(string text)
        {
            // Regex to match attachment tags
            var attachmentPattern = @"<attachment filePath=""([^""]+)"">.*?</attachment>";
            var regex = new Regex(attachmentPattern, RegexOptions.Singleline);

            var result = text;
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                var filePath = match.Groups[1].Value;
                var fileName = Path.GetFileName(filePath);
                
                // Try to find the file in our instruction directories
                var fileContent = await LoadInstructionFileAsync(fileName);
                
                if (fileContent != null)
                {
                    var newAttachment = $"<attachment filePath=\"{filePath}\">{fileContent}</attachment>";
                    result = result.Replace(match.Value, newAttachment);
                }
            }

            return result;
        }

        /// <summary>
        /// Loads an instruction file by matching the filename in the instruction directories.
        /// </summary>
        private static async Task<string?> LoadInstructionFileAsync(string fileName)
        {
            var azsdkToolsInstructionsPath = TestSetup.AzsdkToolsInstructionsPath;
            var customInstructionsPath = TestSetup.CopilotInstructionsPath;

            if (Path.GetFileName(customInstructionsPath) == fileName)
            {
                return await File.ReadAllTextAsync(customInstructionsPath);
            }

            var azsdkFilePath = Path.Combine(azsdkToolsInstructionsPath, fileName);
            if (File.Exists(azsdkFilePath))
            {
                return await File.ReadAllTextAsync(azsdkFilePath);
            }

            return null;
        }

        private static IEnumerable<OpenAIChatMessage> DeserializeMessages(BinaryData data)
        {
            using JsonDocument messagesAsJson = JsonDocument.Parse(data.ToMemory());

            foreach (JsonElement jsonElement in messagesAsJson.RootElement.EnumerateArray())
            {
                yield return ModelReaderWriter.Read<OpenAIChatMessage>(BinaryData.FromObjectAsJson(jsonElement), ModelReaderWriterOptions.Json);
            }
        }

        private static List<ChatMessage> EnsureChatMessageRole(IEnumerable<ChatMessage> translatedMessages, IEnumerable<OpenAIChatMessage> openAIMessages)
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

        public static Dictionary<string, ChatMessage> GetExpectedToolsByName(IEnumerable<ChatMessage> expectedOutcome, IEnumerable<string> toolNames)
        {
            var expectedToolResults = new Dictionary<string, ChatMessage>();

            // Create CallId -> ToolName mapping
            // Tool Name is not available in FunctionResultContent
            // Normalize function names and remove tools used not in toolNames list
            var callIdToName = expectedOutcome
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .Select(fc => new { fc.CallId, Normalized = NormalizeFunctionName(fc.Name, toolNames) })
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
        
        public static string NormalizeFunctionName(string functionName, IEnumerable<string> toolNames)
        {
            // Copilot often prefixes tool names with "mcp_" or similar. 
            // Normalize by looking for tool names that end with the function name. 
            var match = toolNames.FirstOrDefault(name => functionName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            return match;
        }
    }
}
