using Microsoft.Extensions.DependencyInjection;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Services;

namespace Azure.Tools.GeneratorAgent.Composition
{
    public static class DependencyInjection
    {
        public static IServiceProvider Configure()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddScoped<IGeneratorAgentService, GeneratorAgentService>();
            
            return services.BuildServiceProvider();
        }
    }
}