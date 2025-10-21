using Azure.AI.OpenAI;
using Azure.Identity;
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
        
        private static readonly string LocalMcpPowershellScriptPath = 
            Environment.GetEnvironmentVariable("LOCAL_MCP_POWERSHELL_SCRIPT_PATH") 
            ?? throw new InvalidOperationException("LOCAL_MCP_POWERSHELL_SCRIPT_PATH environment variable is required");

        public static readonly string CopilotInstructionsPath = 
            Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_PATH") 
            ?? throw new InvalidOperationException("COPILOT_INSTRUCTIONS_PATH environment variable is required");

        public static readonly string AzsdkToolsInstructionsPath = 
            Environment.GetEnvironmentVariable("AZSDK_TOOLS_INSTRUCTIONS_PATH") 
            ?? throw new InvalidOperationException("AZSDK_TOOLS_INSTRUCTIONS_PATH environment variable is required");

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
