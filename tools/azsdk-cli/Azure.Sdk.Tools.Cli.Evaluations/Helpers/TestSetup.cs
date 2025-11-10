using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.Cli.Evaluations.Helpers
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
        
        private static readonly string relativePathToCli = @"../../../../../tools/azsdk-cli/Azure.Sdk.Tools.Cli";
        public static string? GetCopilotInstructionsPath => Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_PATH");
        public static ChatCompletion GetChatCompletion(IChatClient chatClient, IMcpClient mcpClient) => new ChatCompletion(chatClient, mcpClient);

        public static TokenCredential GetCredential(ILogger? logger = null)
        {
            logger?.LogDebug("Getting Azure credential...");
            var azureService = new AzureService();
            var credential = azureService.GetCredential();
            logger?.LogDebug("Azure credential obtained successfully");
            return credential;
        }

        public static AzureOpenAIClient GetAzureOpenAIClient(ILogger? logger = null)
        {
            var credential = GetCredential(logger);
            return new AzureOpenAIClient(new Uri(AzureOpenAIEndpoint), credential);
        }

        public static IChatClient GetChatClient(ILogger? logger = null)
        {
            var azureClient = GetAzureOpenAIClient(logger);
            return new ChatClientBuilder(azureClient.GetChatClient(AzureOpenAIModelDeploymentName).AsIChatClient())
                .Build();
        }

        public static async Task<IMcpClient> GetMcpClientAsync(ILogger? logger = null)
        {
            logger?.LogDebug("Starting MCP client creation...");
            logger?.LogDebug($"Command: dotnet run --project {relativePathToCli} -- start");

            try
            {
                // Run your local MCP server directly with dotnet run
                var mcpClient = await McpClientFactory.CreateAsync(
                    new StdioClientTransport(
                        new()
                        {
                            Command = "dotnet",
                            Arguments = [
                                "run",
                                "--project",
                                relativePathToCli,
                                "--",
                                "start"
                            ]
                        }
                    )
                );
                logger?.LogDebug("MCP client created successfully");
                return mcpClient;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to create MCP client");
                throw;
            }
        }

        public static void ValidateCopilotEnvironmentConfiguration()
        {
            // Validate that we have at least one valid configuration
            if (string.IsNullOrEmpty(GetCopilotInstructionsPath))
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: COPILOT_INSTRUCTIONS_PATH must be provided.");
            }
        }
    }
}
