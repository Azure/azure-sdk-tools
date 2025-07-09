using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Services;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Logger;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

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
            serviceCollection.AddSingleton<IAppSettings, AppSettings>();
            serviceCollection.AddSingleton<ILoggerService, ConsoleLoggerService>();

            serviceCollection.AddSingleton<PersistentAgentsClient>(sp =>
            {
                var settings = sp.GetRequiredService<IAppSettings>();
                return new PersistentAgentsClient(settings.ProjectEndpoint, new DefaultAzureCredential());
            });

            serviceCollection.AddScoped<IGeneratorAgentService, GeneratorAgentService>();
            
            return serviceCollection.BuildServiceProvider();
        }
    }
}