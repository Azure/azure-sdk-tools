using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace azsdk_mcp_evals
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

        public async Task<ChatResponse> GetChatResponseAsync(IEnumerable<ChatMessage> chat)
        {
            var tools = await _mcpClient.ListToolsAsync();

            var chatOptions =
                new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Text,
                    Tools = [.. tools]
                };

            return await _chatClient.GetResponseAsync(chat, chatOptions);
        }
    }
}
