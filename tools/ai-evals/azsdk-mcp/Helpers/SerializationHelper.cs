using System.Text.Json;
using System.Text.RegularExpressions;
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

            await RebuildAttachmentsInChatMessagesAsync(chatMessages);

            return ParseChatMessagesIntoScenarioData(chatMessages);
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
    }
}
