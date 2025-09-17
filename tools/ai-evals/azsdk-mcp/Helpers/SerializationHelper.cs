using System.Text.Json;
using azsdk_mcp.Models;
using Microsoft.Extensions.AI;

namespace azsdk_mcp.Helpers
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

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var chatMessages = JsonSerializer.Deserialize<List<ChatMessage>>(jsonContent, options);

            if (chatMessages == null || chatMessages.Count == 0)
            {
                throw new InvalidOperationException($"Failed to deserialize chat messages from {jsonPath} or the list is empty");
            }

            return ParseChatMessagesIntoScenarioData(chatMessages);
        }

        private static ScenarioData ParseChatMessagesIntoScenarioData(List<ChatMessage> chatMessages)
        {
            // Find the last user message
            var lastUserMessageIndex = -1;
            for (int i = chatMessages.Count - 1; i >= 0; i--)
            {
                if (chatMessages[i].Role == ChatRole.User)
                {
                    lastUserMessageIndex = i;
                    break;
                }
            }

            if (lastUserMessageIndex == -1)
            {
                throw new InvalidOperationException("No user message found in the chat messages list");
            }

            // Split the messages
            var chatHistory = chatMessages.Take(lastUserMessageIndex).ToList();
            var nextMessage = chatMessages[lastUserMessageIndex];
            var expectedOutcome = chatMessages.Skip(lastUserMessageIndex + 1).ToList();

            return new ScenarioData
            {
                ChatHistory = chatHistory,
                NextMessage = nextMessage,
                ExpectedOutcome = expectedOutcome
            };
        }
    }
}
