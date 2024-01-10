using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class LogStuffWorker : BackgroundService
    {
        private readonly ILogger<LogStuffWorker> _logger;
        private readonly int _instanceNumber;

        public LogStuffWorker(ILogger<LogStuffWorker> logger, int instanceNumber)
        {
            _logger = logger;
            _instanceNumber = instanceNumber;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting the ExecuteAsync method in instance {Instance}", _instanceNumber);
            var delay = Random.Shared.Next(1000, 4000);
            _logger.LogInformation($"Waiting {delay}ms");

            await Task.Delay(delay);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Hello world from instance {Instance}", _instanceNumber);
                await Task.Delay(5000, stoppingToken);
            }

            _logger.LogInformation("Ending the ExecuteAsync method in instance {Instance}", _instanceNumber);
        }
    }
}
