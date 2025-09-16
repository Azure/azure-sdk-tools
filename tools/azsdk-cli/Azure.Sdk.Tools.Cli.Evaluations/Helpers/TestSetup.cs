using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
{
    public static class TestSetup
    {
        // Configuration constants
        private const string AzureOpenAIEndpoint = "https://devex-evals.openai.azure.com/";
        private const string AzureOpenAIModelDeploymentName = "gpt-4o";
        private const string LocalMcpPowershellScriptPath = "..\\..\\..\\..\\..\\eng\\common\\mcp\\azure-sdk-mcp.ps1";

        public static VisualStudioCredential GetCredential()
        {
            return new VisualStudioCredential();
        }

        public static AzureOpenAIClient GetAzureOpenAIClient()
        {
            var credential = GetCredential();
            return new AzureOpenAIClient(new Uri(AzureOpenAIEndpoint), credential);
        }

        public static IChatClient GetChatClient()
        {
            var azureClient = GetAzureOpenAIClient();
            return new ChatClientBuilder(azureClient.GetChatClient(AzureOpenAIModelDeploymentName).AsIChatClient())
                .UseFunctionInvocation()
                .Build();
        }

        public static async Task<IMcpClient> GetMcpClientAsync()
        {
            return await McpClientFactory.CreateAsync(
                new StdioClientTransport(
                    new()
                    {
                        Command = "pwsh",
                        Arguments = [LocalMcpPowershellScriptPath, "-Run"]
                    }
                )
            );
        }

        public static ChatCompletion GetChatCompletion(IChatClient chatClient, IMcpClient mcpClient)
        {
            return new ChatCompletion(chatClient, mcpClient);
        }
    }
}
