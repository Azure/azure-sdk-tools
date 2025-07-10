using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace Azure.Tools.GeneratorAgent
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                cts.Cancel();
                eventArgs.Cancel = true;
            };

            (IConfiguration configuration, ILoggerFactory loggerFactory) = DependencyInjection.Configure();
            ILogger<ErrorFixerAgent> agentLogger = loggerFactory.CreateLogger<ErrorFixerAgent>();
            ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
            IAppSettings appSettings = new AppSettings(configuration);

            PersistentAgentsAdministrationClient adminClient = new PersistentAgentsAdministrationClient(
                new Uri(appSettings.ProjectEndpoint),
                new DefaultAzureCredential());

            try
            {
                await using (ErrorFixerAgent agent = new ErrorFixerAgent(appSettings, agentLogger, adminClient))
                {
                    await agent.InitializeAsync(cts.Token);
                    await agent.FixCodeAsync(cts.Token);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Operation was cancelled. Shutting down gracefully...");
                return 130;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running the Generator Agent");
                return 1;
            }
        }
    }
}

