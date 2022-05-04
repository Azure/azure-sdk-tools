using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Azure.Sdk.Tools.PipelineWitness.Queue.Functions
{
    public class AzurePipelinesBuildCompleteFunction
    {
        private ILogger logger;
        private BlobUploadProcessor runProcessor;
        private readonly TelemetryClient telemetryClient;

        public AzurePipelinesBuildCompleteFunction(ILogger<AzurePipelinesBuildCompleteFunction> logger, BlobUploadProcessor runProcessor, TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.telemetryClient = telemetryClient;
        }

        [FunctionName("AzurePipelinesBuildComplete")]
        public async Task Run([QueueTrigger("%BuildCompleteQueueName%")]QueueMessage message)
        {
            logger.LogInformation("Processing build.complete event.");

            if (message.InsertedOn.HasValue)
            {
                telemetryClient.TrackMetric(new MetricTelemetry
                {
                    Name = "AzurePipelinesBuildComplete MessageLatencyMs",
                    Sum = DateTimeOffset.Now.Subtract(message.InsertedOn.Value).TotalMilliseconds,
                });
            }
            
            logger.LogInformation("Extracting content from message.");

            var devopsEvent = JObject.Parse(message.MessageText);

            var buildUrl = devopsEvent["resource"]?.Value<string>("url");

            if (buildUrl == null)
            {
                this.logger.LogError("Message contained no build url. Message body: {MessageBody}", message.MessageText);
                return;
            }

            var match = Regex.Match(buildUrl, @"^https://dev.azure.com/(?<account>[\w-]+)/(?<project>[0-9a-fA-F-]+)/_apis/build/Builds/(?<build>\d+)$");

            if (!match.Success)
            {
                this.logger.LogError("Message contained an invalid build url: {BuildUrl}", buildUrl);
                return;
            }

            var account = match.Groups["account"].Value;
            var projectIdString = match.Groups["project"].Value;
            var buildIdString = match.Groups["build"].Value;

            if (!Guid.TryParse(projectIdString, out var projectId))
            {
                this.logger.LogError("Could not parse project id as a guid '{ProjectId}'", projectIdString);
                return;
            }

            if (!int.TryParse(buildIdString, out var buildId))
            {
                this.logger.LogError("Could not parse build id as a guid '{BuildId}'", buildIdString);
                return;
            }

            await runProcessor.UploadBuildBlobsAsync(account, projectId, buildId);
        }
    }
}
