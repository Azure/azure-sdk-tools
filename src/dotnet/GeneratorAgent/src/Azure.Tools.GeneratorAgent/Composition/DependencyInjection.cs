using System;
using System.IO;
using System.Reflection;
using Azure.Identity;
using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Tools.GeneratorAgent.Interfaces;

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


            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information);
            });

            // Register configuration
            serviceCollection.AddSingleton(configuration);
            serviceCollection.AddSingleton<IAppSettings>(new AppSettings(configuration));

            // Register logging
            serviceCollection.AddSingleton(loggerFactory);
            serviceCollection.AddLogging();

            return (configuration, loggerFactory);
        }

        private static ILogger<T> CreateLogger<T>(ILoggerFactory loggerFactory)
        {
            return loggerFactory.CreateLogger<T>();
        }
    }
}