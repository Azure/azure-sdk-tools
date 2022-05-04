using System;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineWitness.Queue.Functions
{
    public class AzurePipelinesBuildLogBundleFunction
    {
        private readonly ILogger logger;
        private readonly BlobUploadProcessor runProcessor;
        private readonly TelemetryClient telemetryClient;

        public AzurePipelinesBuildLogBundleFunction(ILogger<AzurePipelinesBuildLogBundleFunction> logger, BlobUploadProcessor runProcessor, TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.telemetryClient = telemetryClient;
        }

        [FunctionName("AzurePipelinesBuildLogBundle")]
        public async Task Run([QueueTrigger("%BuildLogBundlesQueueName%")]QueueMessage message)
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

            logger.LogInformation("Message body was: {messageBody}", message.MessageText);

            BuildLogBundle buildLogBundle;
            try
            {
                logger.LogInformation("Extracting content from message.");
                buildLogBundle = JsonConvert.DeserializeObject<BuildLogBundle>(message.MessageText);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deserialize message body.");
                throw;
            }
                        
            await runProcessor.ProcessBuildLogBundleAsync(buildLogBundle);
        }
    }
}
