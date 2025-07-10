using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace Azure.Tools.GeneratorAgent.Composition
{
    public static class DependencyInjection
    {
        public static IServiceProvider Configure()
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            // Get the directory where the tool is installed
            string toolDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
                ?? throw new InvalidOperationException("Unable to determine tool installation directory");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(toolDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            serviceCollection.AddSingleton<IConfiguration>(configuration);
            serviceCollection.AddSingleton<IAppSettings, AppSettings>();
            
            // Add logging with console provider
            serviceCollection.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"))
                       .AddConsole()
                       .SetMinimumLevel(LogLevel.Information);
            });

            serviceCollection.AddSingleton<PersistentAgentsClient>(sp =>
            {
                var settings = sp.GetRequiredService<IAppSettings>();
                return new PersistentAgentsClient(settings.ProjectEndpoint, new DefaultAzureCredential());
            });

            serviceCollection.AddScoped<ErrorFixerAgent>();

            return serviceCollection.BuildServiceProvider();
        }
    }
}