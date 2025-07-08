using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.Tools.GeneratorAgent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            IServiceProvider provider = DependencyInjection.Configure();

            IGeneratorAgentService agent = provider.GetRequiredService<IGeneratorAgentService>();
            
            await agent.DeleteAgentsAsync(CancellationToken.None);

            await agent.CreateAgentAsync(CancellationToken.None);
        }
    }
}

