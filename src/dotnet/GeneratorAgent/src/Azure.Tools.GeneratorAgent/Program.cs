using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Logger;
using Azure.Tools.GeneratorAgent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Tools.GeneratorAgent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            IServiceProvider provider = DependencyInjection.Configure();
            ILoggerService logger = provider.GetRequiredService<ILoggerService>();

            try
            {
                await using (var agent = provider.GetRequiredService<GeneratorAgentService>())
                {
                    await agent.InitializeAsync(CancellationToken.None);
                    // TODO: Add your code analysis and fix logic here
                    await agent.FixCodeAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error occurred while running the Generator Agent: " + ex.Message);;
            }
        }
    }
}

