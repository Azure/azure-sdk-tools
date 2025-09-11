using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Azure.Sdk.Tools.Cli.Evaluations
{
    public class Program
    {
        private const string AzureOpenAIEndpoint = "https://devex-evals.openai.azure.com/";
        private const string AzureOpenAIModelDeploymentName = "gpt-4o";
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder();

            // Use appropriate credential based on environment
            var credential = new VisualStudioCredential();

            builder.Services.AddChatClient(sp =>
                new AzureOpenAIClient(new Uri(AzureOpenAIEndpoint), credential)
                    .GetChatClient(AzureOpenAIModelDeploymentName)
                    .AsIChatClient()
            )/*.UseDistributedCache()*/;

            var mcpClient = await McpClientFactory.CreateAsync(
                new StdioClientTransport(
                    new()
                    {
                        Command = "pwsh",
                        Arguments = ["C:\\Users\\juanospina\\source\\repos\\azure-sdk-tools\\eng\\common\\mcp\\azure-sdk-mcp.ps1", "-Run"]
                    }
                )
            );
            builder.Services.AddSingleton<IMcpClient>(sp => mcpClient);

            builder.Services.AddSingleton<ChatCompletion>();
            builder.Services.AddSingleton<ChatConfiguration>();
            builder.Services.AddSingleton<Scenario>();

            var host = builder.Build();

            // Get the scenario service and run it
            var scenario = host.Services.GetRequiredService<Scenario>();

            var jsonPath = "C:\\Users\\juanospina\\source\\repos\\azure-sdk-tools\\tools\\ai-evals\\azsdk-mcp-evals\\example.json";
            await scenario.PlayAsync(jsonPath);
        }
    }
}

