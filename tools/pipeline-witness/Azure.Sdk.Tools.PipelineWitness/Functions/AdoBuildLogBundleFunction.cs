using System;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;

using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.PipelineWitness.Queue.Functions
{
    public class AdoBuildLogBundleFunction
    {
        public AdoBuildLogBundleFunction(ILogger<AdoBuildLogBundleFunction> logger, BlobUploadProcessor runProcessor)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
        }

        private ILogger logger;
        private BlobUploadProcessor runProcessor;

        [FunctionName("AdoBuildLogBundle")]
        public async Task Run([QueueTrigger("%BuildLogBundlesQueueName%")]QueueMessage message)
        {
            logger.LogInformation("Processing build log bundle message.");
            
            var messageBody = message.MessageText;
            logger.LogInformation("Message body was: {messageBody}", messageBody);


            BuildLogBundle buildLogBundle;

            try
            {
                logger.LogInformation("Extracting content from message.");
                buildLogBundle = JsonConvert.DeserializeObject<BuildLogBundle>(messageBody);
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
