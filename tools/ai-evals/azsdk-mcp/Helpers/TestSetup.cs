using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.McpEvals.Helpers
{
    public static class TestSetup
    {
        // Configuration from environment variables - required
        private static readonly string AzureOpenAIEndpoint = 
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is required");
        
        private static readonly string AzureOpenAIModelDeploymentName = 
            Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_DEPLOYMENT_NAME") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_MODEL_DEPLOYMENT_NAME environment variable is required");

        public static readonly string CopilotInstructionsPath = 
            Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_PATH") 
            ?? throw new InvalidOperationException("COPILOT_INSTRUCTIONS_PATH environment variable is required");

        public static TokenCredential GetCredential()
        {
            var azureService = new AzureService();
            return azureService.GetCredential();
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
                .Build();
        }

        public static async Task<IMcpClient> GetMcpClientAsync()
        {
            // Run your local MCP server directly with dotnet run
            return await McpClientFactory.CreateAsync(
                new StdioClientTransport(
                    new()
                    {
                        Command = "dotnet",
                        Arguments = [
                            "run",
                            "--project",
                            @"..\..\..\..\..\tools\azsdk-cli\Azure.Sdk.Tools.Cli",
                            "--",
                            "start" 
                        ]
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
