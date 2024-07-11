using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions
{
    internal class GitHubActionsRunQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger logger;
        private readonly GitHubActionProcessor processor;

        public GitHubActionsRunQueueWorker(
            ILogger<GitHubActionsRunQueueWorker> logger,
            GitHubActionProcessor processor,
            QueueServiceClient queueServiceClient,
            TelemetryClient telemetryClient,
            IOptionsMonitor<PipelineWitnessSettings> options)
            : base(
                logger,
                telemetryClient,
                queueServiceClient,
                options.CurrentValue.GitHubActionRunsQueueName,
                options)
        {
            this.logger = logger;
            this.processor = processor;
        }

        internal override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Processing build.complete event: {MessageText}", message.MessageText);

            var githubMessage = JsonSerializer.Deserialize<GitHubRunCompleteMessage>(message.MessageText);

            await this.processor.ProcessAsync(githubMessage.Owner, githubMessage.Repository, githubMessage.RunId);
        }
    }
}
