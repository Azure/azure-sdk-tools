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
        public static string? GetRepoName => Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_REPOSITORY_NAME");
        public static string? GetRepoOwner => Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_REPOSITORY_OWNER");
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
            var repoName = GetRepoName;
            var repoOwner = GetRepoOwner;
            var copilotInstructionsPath = GetCopilotInstructionsPath;
            bool hasRepoName = !string.IsNullOrEmpty(repoName);
            bool hasRepoOwner = !string.IsNullOrEmpty(repoOwner);
            bool hasCopilotPath = !string.IsNullOrEmpty(copilotInstructionsPath);

            // Check if both repo name and owner are provided
            bool hasRepoInfo = hasRepoName && hasRepoOwner;

            // Validate that we have at least one valid configuration
            if (!hasRepoInfo && !hasCopilotPath)
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: Either both COPILOT_INSTRUCTIONS_REPOSITORY_NAME and " +
                    "COPILOT_INSTRUCTIONS_REPOSITORY_OWNER must be provided, OR COPILOT_INSTRUCTIONS_PATH must be provided.");
            }

            // If repo info is partially provided, it's also an error
            if (hasRepoOwner ^ hasRepoName)
            {
                throw new InvalidOperationException(
                    "Invalid repository configuration: Both COPILOT_INSTRUCTIONS_REPOSITORY_NAME and " +
                    "COPILOT_INSTRUCTIONS_REPOSITORY_OWNER must be provided together.");
            }
        }
    }
}
