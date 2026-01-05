using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Responses;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    public class ChatCompletion
    {
        private IChatClient _chatClient;
        private IMcpClient _mcpClient;

        public ChatCompletion(IChatClient chatClient, IMcpClient mcpClient)
        {
            _chatClient = chatClient;
            _mcpClient = mcpClient;
        }

        public async Task<ChatResponse> GetChatResponseWithExpectedResponseAsync(
            IEnumerable<ChatMessage> chat, 
            Dictionary<string, ChatMessage> expectedToolResults,
            IEnumerable<string> optionalToolNames)
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
            var optionalCallIds = new HashSet<string>();

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

                    if(optionalToolNames.Contains(functionCall.Name))
                    {
                        optionalCallIds.Add(functionCall.CallId);
                    }
                }

                response = await _chatClient.GetResponseAsync(conversationMessages, chatOptions);
            }

            // Add the final assistant message (when there are no further tool calls)
            var finalAssistantMessage = response.Messages.FirstOrDefault();
            if (finalAssistantMessage != null)
            {
                conversationMessages.Add(finalAssistantMessage);
            }

            // Filter out any optional tool calls and their corresponding tool results
            var filtered = FilterOptionalToolResponses(conversationMessages.Skip(chatInitialIndex), optionalCallIds);
            return new ChatResponse([.. filtered]);
        }

        private IEnumerable<ChatMessage> FilterOptionalToolResponses(IEnumerable<ChatMessage> messages, HashSet<string> optionalCallIds)
        {
            // No optional calls to filter. 
            if(optionalCallIds.Count == 0)
            {
                return messages;
            }

            foreach (var message in messages)
            {
                var functionCalls = message.Contents.OfType<FunctionCallContent>();
                var functionResults = message.Contents.OfType<FunctionResultContent>();

                // Remove optional tool calls and results.
                message.Contents = [.. message.Contents.Where(content =>
                    !(content is FunctionCallContent fc && optionalCallIds.Contains(fc.CallId)) &&
                    !(content is FunctionResultContent fr && optionalCallIds.Contains(fr.CallId))
                )];

                if (message.Contents.Any())
                {
                    yield return message;
                }
            }
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
