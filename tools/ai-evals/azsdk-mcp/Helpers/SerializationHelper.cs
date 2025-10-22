using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.McpEvals.Models;
using Microsoft.Extensions.AI;
using MicrosoftExtensionsAIChatExtensions = OpenAI.Chat.MicrosoftExtensionsAIChatExtensions;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

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
            var fixedChatMessages = ChatMessageHelper.EnsureChatMessageRole(translatedChatMessages, chatMessages);

            await RebuildAttachmentsInChatMessagesAsync(fixedChatMessages);

            return ChatMessageHelper.ParseChatMessagesIntoScenarioData(fixedChatMessages);
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
        
        public static string NormalizeFunctionName(string functionName, IEnumerable<string> toolNames)
        {
            // Copilot often prefixes tool names with "mcp_" or similar. 
            // Normalize by looking for tool names that end with the function name. 
            var match = toolNames.FirstOrDefault(name => functionName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            return match;
        }
    }
}
