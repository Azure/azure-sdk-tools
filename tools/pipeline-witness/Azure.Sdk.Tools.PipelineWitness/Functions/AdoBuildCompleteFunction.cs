using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Azure.Sdk.Tools.PipelineWitness.Queue.Functions
{
    public class AdoBuildCompleteFunction
    {
        public AdoBuildCompleteFunction(ILogger<AdoBuildCompleteFunction> logger, BlobUploadProcessor runProcessor)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
        }

        private ILogger logger;
        private BlobUploadProcessor runProcessor;

        [FunctionName("AdoBuildComplete")]
        public async Task Run([QueueTrigger("ado-build-completed")]QueueMessage message)
        {
            logger.LogInformation("Processing build.complete event.");
            var messageBody = message.MessageText;
            logger.LogInformation("Message body was: {messageBody}", messageBody);

            logger.LogInformation("Extracting content from message.");

            var devopsEvent = JObject.Parse(messageBody);

            var buildUrl = devopsEvent["resource"]?.Value<string>("url");

            if (buildUrl == null)
            {
                this.logger.LogError("Message contained no build url. Message body: {MessageBody}", messageBody);
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
