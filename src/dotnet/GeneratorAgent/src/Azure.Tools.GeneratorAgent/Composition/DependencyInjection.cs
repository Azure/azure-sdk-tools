using Microsoft.Extensions.DependencyInjection;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Services;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;

namespace Azure.Tools.GeneratorAgent.Composition
{
    public static class DependencyInjection
    {
        public static IServiceProvider Configure()
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            serviceCollection.AddSingleton<IConfiguration>(configuration);
            serviceCollection.AddSingleton<AppSettings>();
            serviceCollection.AddScoped<IGeneratorAgentService, GeneratorAgentService>();
            
            return serviceCollection.BuildServiceProvider();
        }
    }
}