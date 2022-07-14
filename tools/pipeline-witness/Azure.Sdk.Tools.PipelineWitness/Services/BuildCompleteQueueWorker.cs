using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ILogger _logger;
        private readonly BlobUploadProcessor _runProcessor;

        public BuildCompleteQueueWorker(
            ILogger<BuildCompleteQueueWorker> logger,
            BlobUploadProcessor runProcessor,
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
            _logger = logger;
            _runProcessor = runProcessor;
        }

        protected override async Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing build.complete event.");

            var devopsEvent = JObject.Parse(message.MessageText);

            var buildUrl = devopsEvent["resource"]?.Value<string>("url");

            if (buildUrl == null)
            {
                _logger.LogError("Message contained no build url. Message body: {MessageBody}", message.MessageText);
                return;
            }

            var match = Regex.Match(buildUrl, @"^https://dev.azure.com/(?<account>[\w-]+)/(?<project>[0-9a-fA-F-]+)/_apis/build/Builds/(?<build>\d+)$");

            if (!match.Success)
            {
                _logger.LogError("Message contained an invalid build url: {BuildUrl}", buildUrl);
                return;
            }

            var account = match.Groups["account"].Value;
            var projectIdString = match.Groups["project"].Value;
            var buildIdString = match.Groups["build"].Value;

            if (!Guid.TryParse(projectIdString, out var projectId))
            {
                _logger.LogError("Could not parse project id as a guid '{ProjectId}'", projectIdString);
                return;
            }

            if (!int.TryParse(buildIdString, out var buildId))
            {
                _logger.LogError("Could not parse build id as a guid '{BuildId}'", buildIdString);
                return;
            }

            await _runProcessor.UploadBuildBlobsAsync(account, projectId, buildId);
        }
    }
}
