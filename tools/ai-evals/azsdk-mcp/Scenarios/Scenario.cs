using azsdk_mcp.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Reflection;

namespace azsdk_mcp.Scenarios
{
    [TestClass]
    public partial class Scenario
    {
        public TestContext TestContext { get; set; } = null!;

        // Static services
        protected static IChatClient? ChatClient;
        protected static IMcpClient? McpClient;
        protected static ChatCompletion? ChatCompletion;

        [AssemblyInitialize]
        public static async Task AssemblyInitialize(TestContext context)
        {
            ChatClient = TestSetup.GetChatClient();
            McpClient = await TestSetup.GetMcpClientAsync();
            ChatCompletion = TestSetup.GetChatCompletion(ChatClient, McpClient);
        }
    }
}
