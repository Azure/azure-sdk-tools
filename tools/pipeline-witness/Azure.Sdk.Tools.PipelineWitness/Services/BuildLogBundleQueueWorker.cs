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
using Azure.Sdk.Tools.PipelineWitness.Entities;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal class BuildLogBundleQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobUploadProcessor _runProcessor;

        public BuildLogBundleQueueWorker(
            ILogger<BuildLogBundleQueueWorker> logger,
            BlobUploadProcessor runProcessor,
            QueueServiceClient queueServiceClient,
            TelemetryClient telemetryClient,
            IOptionsMonitor<PipelineWitnessSettings> options)
            : base(
                  logger,
                  telemetryClient,
                  queueServiceClient,
                  options?.CurrentValue?.BuildLogBundlesQueueName,
                  options)
        {
            _logger = logger;
            _runProcessor = runProcessor;
            _telemetryClient = telemetryClient;
        }

        protected override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing build log bundle message.");

            if (message.InsertedOn.HasValue)
            {
                _telemetryClient.TrackMetric(new MetricTelemetry
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
                _logger.LogError(ex, "Failed to deserialize message body. Body: {MessageBody}", message.MessageText);
                throw;
            }

            // TODO: Add cancellation token propagation
            await _runProcessor.ProcessBuildLogBundleAsync(buildLogBundle);
        }
    }
}
