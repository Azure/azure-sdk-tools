using Azure.Sdk.Tools.McpEvals.Helpers;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using NUnit.Framework;

namespace Azure.Sdk.Tools.McpEvals.Scenarios
{
    [TestFixture]
    public partial class Scenario
    {
        // Static services shared across all tests
        protected static IChatClient? ChatClient;
        protected static IMcpClient? McpClient;
        protected static ChatCompletion? ChatCompletion;

        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            ChatClient = TestSetup.GetChatClient();
            McpClient = await TestSetup.GetMcpClientAsync();
            ChatCompletion = TestSetup.GetChatCompletion(ChatClient, McpClient);
        }
    }
}
