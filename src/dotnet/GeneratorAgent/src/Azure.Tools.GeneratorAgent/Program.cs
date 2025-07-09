using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                cts.Cancel();
                eventArgs.Cancel = true;
            };

            IServiceProvider provider = DependencyInjection.Configure();
            ILogger<Program> logger = provider.GetRequiredService<ILogger<Program>>();

            try
            {
                await using (var agent = provider.GetRequiredService<GeneratorAgentService>())
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

