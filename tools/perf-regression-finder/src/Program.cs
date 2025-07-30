using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace perf_semantic_kernel
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration from appsettings.json
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Initialize and run the agent provider
            var semanticAzureAi = new SemanticAzureAi(config);
            await semanticAzureAi.RunChatLoopAsync();


        }
    }
}
