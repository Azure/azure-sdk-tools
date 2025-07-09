using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Logger;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Tools.GeneratorAgent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            IServiceProvider provider = DependencyInjection.Configure();

            ILoggerService logger = provider.GetRequiredService<ILoggerService>();

            IGeneratorAgentService agent = provider.GetRequiredService<IGeneratorAgentService>();

            try
            {
                await agent.DeleteAgentsAsync(CancellationToken.None);
                await agent.CreateAgentAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running the Generator Agent");
            }
        }
    }
}

