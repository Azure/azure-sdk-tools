using System;
using System.IO;
using System.Reflection;
using Azure.Identity;
using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Composition
{
    public static class DependencyInjection
    {
        public static (IConfiguration Configuration, ILoggerFactory LoggerFactory) Configure()
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            string toolDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
                ?? throw new InvalidOperationException("Unable to determine tool installation directory");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(toolDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable(EnvironmentVariables.EnvironmentName) ?? "Development"}.json", optional: true)
                .AddEnvironmentVariables(EnvironmentVariables.Prefix)
                .Build();

            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddSingleton(new AppSettings(configuration));

            serviceCollection.AddLogging(builder =>
                builder
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information));

            // Build the service provider
            var serviceProvider = serviceCollection.BuildServiceProvider();

            return (
                serviceProvider.GetRequiredService<IConfiguration>(),
                serviceProvider.GetRequiredService<ILoggerFactory>()
            );
        }

        private static ILogger<T> CreateLogger<T>(ILoggerFactory loggerFactory)
        {
            return loggerFactory.CreateLogger<T>();
        }
    }
}