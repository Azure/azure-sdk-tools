using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal class BuildCompleteQueueWorker : QueueWorkerBackgroundService
    {
        private readonly ILogger logger;
        private readonly Func<BlobUploadProcessor> runProcessorFactory;

        public BuildCompleteQueueWorker(
            ILogger<BuildCompleteQueueWorker> logger,
            Func<BlobUploadProcessor> runProcessorFactory,
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
            this.runProcessorFactory = runProcessorFactory;
        }

        internal override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Processing build.complete event: {MessageText}", message.MessageText);

            JObject devopsEvent = JObject.Parse(message.MessageText);

            string buildUrl = devopsEvent["resource"]?.Value<string>("url");

            if (buildUrl == null)
            {
                this.logger.LogError("Message contained no build url. Message body: {MessageBody}", message.MessageText);
                return;
            }

            Match match = Regex.Match(buildUrl, @"^https://dev.azure.com/(?<account>[\w-]+)/(?<project>[0-9a-fA-F-]+)/_apis/build/Builds/(?<build>\d+)$");

            if (!match.Success)
            {
                this.logger.LogError("Message contained an invalid build url: {BuildUrl}", buildUrl);
                return;
            }

            string account = match.Groups["account"].Value;
            string projectIdString = match.Groups["project"].Value;
            string buildIdString = match.Groups["build"].Value;

            if (!Guid.TryParse(projectIdString, out Guid projectId))
            {
                this.logger.LogError("Could not parse project id as a guid '{ProjectId}'", projectIdString);
                return;
            }

            if (!int.TryParse(buildIdString, out int buildId))
            {
                this.logger.LogError("Could not parse build id as a guid '{BuildId}'", buildIdString);
                return;
            }

            BlobUploadProcessor runProcessor = this.runProcessorFactory.Invoke();

            await runProcessor.UploadBuildBlobsAsync(account, projectId, buildId);
        }
    }
}
