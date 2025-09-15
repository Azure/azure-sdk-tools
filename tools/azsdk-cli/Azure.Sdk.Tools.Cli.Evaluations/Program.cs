using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Azure.Sdk.Tools.Cli.Evaluations.Helpers;
using Azure.Sdk.Tools.Cli.Evaluations.Scenarios;

namespace Azure.Sdk.Tools.Cli.Evaluations
{
    public class Program
    {
        // TODO: Move these constants to environment variables.
        private const string _azureOpenAIEndpoint = "https://devex-evals.openai.azure.com/";
        private const string _azureOpenAIModelDeploymentName = "gpt-4o";
        private const string _localMcpPowershellScriptPath = "..\\..\\..\\..\\..\\eng\\common\\mcp\\azure-sdk-mcp.ps1";
        private const string _jsonPath = "..\\..\\..\\..\\..\\tools\\azsdk-cli\\Azure.Sdk.Tools.Cli.Evaluations\\example.json";

        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder();

            // TODO: improve auth, using VS credential for now
            var credential = new VisualStudioCredential();

            builder.Services.AddChatClient(sp =>
            {
                var azureClient = new AzureOpenAIClient(new Uri(_azureOpenAIEndpoint), credential)
                    .GetChatClient(_azureOpenAIModelDeploymentName)
                    .AsIChatClient();
                return new ChatClientBuilder(azureClient)
                    .UseFunctionInvocation() // Need to setup the Chat Client Builder so that it can support function invocation for tool calls.
                    .Build();
            }
            // TODO: Enable distributed cache
            )/*.UseDistributedCache()*/;

            var mcpClient = await McpClientFactory.CreateAsync(
                new StdioClientTransport(
                    new()
                    {
                        Command = "pwsh",
                        Arguments = [_localMcpPowershellScriptPath, "-Run"]
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

            await scenario.PlayAsync(_jsonPath);
        }
    }
}

