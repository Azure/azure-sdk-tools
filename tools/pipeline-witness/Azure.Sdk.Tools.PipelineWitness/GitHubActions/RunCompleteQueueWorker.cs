using System;
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
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions
{
    internal class RunCompleteQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger logger;
        private readonly GitHubActionProcessor processor;
        private readonly GitHubClientFactory githubClientFactory;

        public RunCompleteQueueWorker(
            ILogger<RunCompleteQueueWorker> logger,
            GitHubActionProcessor processor,
            QueueServiceClient queueServiceClient,
            GitHubClientFactory githubClientFactory,
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
            this.githubClientFactory = githubClientFactory;
        }

        internal override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Processing build.complete event: {MessageText}", message.MessageText);

            var githubMessage = JsonSerializer.Deserialize<RunCompleteQueueMessage>(message.MessageText);

            try
            {
                await this.processor.ProcessAsync(githubMessage.Owner, githubMessage.Repository, githubMessage.RunId);
            }
            catch(RateLimitExceededException ex)
            {
                this.logger.LogError(ex, "Rate limit exceeded while processing run {RunId}", githubMessage.RunId);

                try
                {
                    var client = await this.githubClientFactory.CreateGitHubClientAsync();
                    var rateLimit = await client.RateLimit.GetRateLimits();
                    this.logger.LogInformation("Rate limit details: {RateLimit}", JsonSerializer.Serialize(rateLimit.Resources));
                }
                catch (Exception rateLimitException)
                {
                    this.logger.LogError(rateLimitException, "Error logging rate limit details");
                }

                var resetRemaining = ex.Reset - DateTimeOffset.UtcNow;

                throw new PauseProcessingException(resetRemaining);
            }

        }
    }
}
