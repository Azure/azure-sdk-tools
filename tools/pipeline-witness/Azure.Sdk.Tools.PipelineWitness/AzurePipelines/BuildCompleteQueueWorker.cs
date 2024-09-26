using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Controllers;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    internal class BuildCompleteQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger logger;
        private readonly Func<AzurePipelinesProcessor> processorFactory;

        public BuildCompleteQueueWorker(
            ILogger<BuildCompleteQueueWorker> logger,
            Func<AzurePipelinesProcessor> processorFactory,
            QueueServiceClient queueServiceClient,
            TelemetryClient telemetryClient,
            IOptionsMonitor<PipelineWitnessSettings> options)
            : base(
                logger,
                telemetryClient,
                queueServiceClient,
                options.CurrentValue.BuildCompleteQueueName,
                options)
        {
            this.logger = logger;
            this.processorFactory = processorFactory;
        }

        internal override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Processing build.complete event: {MessageText}", message.MessageText);

            BuildCompleteQueueMessage queueMessage;

            if (message.MessageText.Contains("_apis/build/Builds"))
            {
                // Legacy message format. Parsing is now done in the DevopsEventsController. Use the controler to convert it.
                if (!DevopsEventsController.TryConvertMessage(JsonDocument.Parse(message.MessageText), out queueMessage))
                {
                    this.logger.LogError("Failed to convert legacy message: {MessageText}", message.MessageText);
                    return;
                }
            }
            else
            {
                queueMessage = JsonSerializer.Deserialize<BuildCompleteQueueMessage>(message.MessageText);
            }

            if (string.IsNullOrEmpty(queueMessage.Account) || queueMessage.ProjectId == Guid.Empty || queueMessage.BuildId == 0)
            {
                this.logger.LogError("Failed to deserialize message: {MessageText}", message.MessageText);
                return;
            }
            var processor = this.processorFactory();
            await processor.UploadBuildBlobsAsync(queueMessage.Account, queueMessage.ProjectId, queueMessage.BuildId);
        }
    }
}
