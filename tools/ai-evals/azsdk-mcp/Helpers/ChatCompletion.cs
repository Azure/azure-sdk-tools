using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Azure.Sdk.Tools.McpEvals.Helpers
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

        public async Task<ChatResponse> GetChatResponseAsync(IEnumerable<ChatMessage> chat, int maxToolCalls)
        {
            var tools = await _mcpClient.ListToolsAsync();
            var result = new List<ChatResponseUpdate>();
            var toolsCalled = new HashSet<string>();

            var chatOptions =
                new ChatOptions
                {
                    Tools = [.. tools]
                };

            // GetResponseAsync allows the LLM to do too much. Limit it to the number of tools we expect
            // Not including calling a tool again. 
            await foreach(var message in _chatClient.GetStreamingResponseAsync(chat, chatOptions))
            {
                foreach(var content in message.Contents)
                {
                    if (content is FunctionCallContent func)
                    {
                        toolsCalled.Add(func.Name);
                    }
                }

                if(message.Contents.Any())
                {
                    result.Add(message);
                }
                
                
                if(toolsCalled.Count >= maxToolCalls)
                {
                    break;
                }
            }

            return result.ToChatResponse();
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
