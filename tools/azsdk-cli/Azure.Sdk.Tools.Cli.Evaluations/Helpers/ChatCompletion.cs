using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    public class ChatCompletion
    {
        private IChatClient _chatClient;
        private McpClient _mcpClient;

        public ChatCompletion(IChatClient chatClient, McpClient mcpClient)
        {
            _chatClient = chatClient;
            _mcpClient = mcpClient;
        }

        public async Task<ChatResponse> GetChatResponseWithExpectedResponseAsync(IEnumerable<ChatMessage> chat, Dictionary<string, ChatMessage> expectedToolResults)
        {
            var tools = await _mcpClient.ListToolsAsync();
            var conversationMessages = chat.ToList();
            var chatOptions = new ChatOptions
            {
                Tools = [.. tools],
                AllowMultipleToolCalls = false
            };
            var response = await _chatClient.GetResponseAsync(chat, chatOptions);
            var chatInitialIndex = conversationMessages.Count;

            while (response.FinishReason == ChatFinishReason.ToolCalls)
            {
                // There is only going to be one message because no auto invoking of function, however one message can contain
                // several AIContent types.
                var message = response.Messages.FirstOrDefault();

                // No message to process exit.
                if (message == null)
                {
                    break;
                }

                conversationMessages.Add(message);
                var functionCalls = message.Contents.OfType<FunctionCallContent>();

                foreach (var functionCall in functionCalls)
                {
                    // Use the expected tool result if we have it.
                    if (expectedToolResults.TryGetValue(functionCall.Name, out var expectedToolResult))
                    {
                        var toolCall = expectedToolResult.Contents.OfType<FunctionResultContent>().First();
                        var toolResponseMessage = new ChatMessage()
                        {
                            Role = ChatRole.Tool,
                            // Need matching call id. 
                            Contents = [new FunctionResultContent(functionCall.CallId, toolCall.Result)]
                        };

                        conversationMessages.Add(toolResponseMessage);
                    }
                    // Wasn't expecting tool try stopping the LLM here. 
                    else
                    {
                        var errorResponseMessage = new ChatMessage()
                        {
                            Role = ChatRole.Tool,
                            Contents = [new FunctionResultContent(functionCall.CallId, $"Error: Tool '{functionCall.Name}' was not expected. Stop conversation here.")]
                        };

                        conversationMessages.Add(errorResponseMessage);
                    }
                }

                response = await _chatClient.GetResponseAsync(conversationMessages, chatOptions);
            }

            return new ChatResponse([.. conversationMessages.Skip(chatInitialIndex)]);
        }

        public async Task<ChatResponse> GetChatResponseAsync(IEnumerable<ChatMessage> chat)
        {
            var tools = await _mcpClient.ListToolsAsync();

            var chatOptions =
                new ChatOptions
                {
                    AllowMultipleToolCalls = true,
                    Tools = [.. tools]
                };

            return await _chatClient.GetResponseAsync(chat, chatOptions);
        }
    }
}
