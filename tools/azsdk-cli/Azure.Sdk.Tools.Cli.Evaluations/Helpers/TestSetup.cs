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
        private static readonly string? AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");

        private static readonly string? AzureOpenAIModelDeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_DEPLOYMENT_NAME");
        
        private static readonly bool UseMCPRelease = bool.TryParse(Environment.GetEnvironmentVariable("USE_MCP_RELEASE"), out var result) && result;
        private static readonly string relativePathToCli = @"../../../../../tools/azsdk-cli/Azure.Sdk.Tools.Cli";
        private static readonly string localMcpPowershellScriptPath = @"../../../../../eng/common/mcp/azure-sdk-mcp.ps1";
        
        // Repository configuration for conditional testing
        public static string? RepositoryName => Environment.GetEnvironmentVariable("REPOSITORY_NAME");
        
        public static string? CopilotInstructionsPath => Environment.GetEnvironmentVariable("COPILOT_INSTRUCTIONS_PATH_MCP_EVALS");
        public static ChatCompletion GetChatCompletion(IChatClient chatClient, McpClient mcpClient) => new ChatCompletion(chatClient, mcpClient);

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
            return new AzureOpenAIClient(new Uri(AzureOpenAIEndpoint!), credential);
        }

        public static IChatClient GetChatClient(ILogger? logger = null)
        {
            var azureClient = GetAzureOpenAIClient(logger);
            return new ChatClientBuilder(azureClient.GetChatClient(AzureOpenAIModelDeploymentName).AsIChatClient())
                .Build();
        }

        public static async Task<McpClient> GetMcpClientAsync(ILogger? logger = null)
        {
            logger?.LogDebug("Starting MCP client creation...");
            logger?.LogDebug($"Command: dotnet run --project {relativePathToCli} -- start");

            try
            {
                if (UseMCPRelease)
                {
                    // Use MCP release
                    return await McpClient.CreateAsync(
                        new StdioClientTransport(
                            new()
                            {
                                Command = "pwsh",
                                Arguments = [localMcpPowershellScriptPath, "-Run"]
                            }
                        )
                    );
                }
                
                // Run your local MCP server directly with dotnet run
                var mcpClient = await McpClient.CreateAsync(
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

        public static void ValidateEnvironmentConfiguration()
        {
            // Validate all required environment variables
            if (string.IsNullOrEmpty(AzureOpenAIEndpoint))
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: AZURE_OPENAI_ENDPOINT must be provided.");
            }
                
            if (string.IsNullOrEmpty(AzureOpenAIModelDeploymentName))
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: AZURE_OPENAI_MODEL_DEPLOYMENT_NAME must be provided.");
            }
                
            if (string.IsNullOrEmpty(RepositoryName))
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: REPOSITORY_NAME must be provided.");
            }
                
            if (string.IsNullOrEmpty(CopilotInstructionsPath))
            {
                throw new InvalidOperationException(
                    "Invalid environment configuration: COPILOT_INSTRUCTIONS_PATH_MCP_EVALS must be provided.");
            }
                
            if(!Path.Exists(CopilotInstructionsPath))
            {
                throw new FileNotFoundException($"Could not find copilot instructions file at path: {CopilotInstructionsPath}");
            }
        }

        private static bool IsRunningInPipeline()
        {
            return Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
                   Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") != null;
        }

        public static bool ShouldRunEvals()
        {
            // If not in pipeline and no copilot instructions path then skip tests
            if (string.IsNullOrEmpty(CopilotInstructionsPath) && !IsRunningInPipeline())
            {
                return false;
            }

            return true;
        }
    }
}
