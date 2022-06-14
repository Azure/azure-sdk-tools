using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal class BuildLogBundleQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger logger;
        private readonly TelemetryClient telemetryClient;
        private readonly BlobUploadProcessor runProcessor;

        public BuildLogBundleQueueWorker(
            ILogger<BuildLogBundleQueueWorker> logger,
            BlobUploadProcessor runProcessor,
            QueueServiceClient queueServiceClient,
            TelemetryClient telemetryClient,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  telemetryClient,
                  queueServiceClient,
                  options?.Value?.BuildLogBundlesQueueName,
                  options)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.telemetryClient = telemetryClient;
        }

        internal override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            logger.LogInformation("Processing build log bundle message.");

            if (message.InsertedOn.HasValue)
            {
                telemetryClient.TrackMetric(new MetricTelemetry
                {
                    Name = "AzurePipelinesBuildLogBundle MessageLatencyMs",
                    Sum = DateTimeOffset.Now.Subtract(message.InsertedOn.Value).TotalMilliseconds,
                });
            }

            BuildLogBundle buildLogBundle;
            try
            {
                buildLogBundle = JsonConvert.DeserializeObject<BuildLogBundle>(message.MessageText);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize message body. Body: {MessageBody}", message.MessageText);
                throw;
            }

            // TODO: Add cancellation token propatagion
            await runProcessor.ProcessBuildLogBundleAsync(buildLogBundle);
        }
    }
}
