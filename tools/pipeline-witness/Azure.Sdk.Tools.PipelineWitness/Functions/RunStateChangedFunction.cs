using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Functions
{
    public class RunStateChangedFunction
    {
        public RunStateChangedFunction(ILogger<RunStateChangedFunction> logger, RunProcessor runProcessor)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
        }

        private ILogger logger;
        private RunProcessor runProcessor;

        [FunctionName("RunStateChanged")]
        public async Task Run([EventHubTrigger("run-state-changed", Connection = "PipelineWitnessEventHubConnectionString", ConsumerGroup = "%EventHubConsumerGroup%")]EventData @event)
        {
            logger.LogInformation("Processing run-state-changed event.");
            string messageBody = Encoding.UTF8.GetString(@event.Body.Span);
            logger.LogInformation("Message body was: {messageBody}", messageBody);

            logger.LogInformation("Extracting content from message.");
            var message = JsonDocument.Parse(messageBody);
            var base64EncodedJson = message.RootElement.GetProperty("content").ToString();
            var jsonBytes = Convert.FromBase64String(base64EncodedJson);
            var json = Encoding.UTF8.GetString(jsonBytes);
            logger.LogInformation("Payload: {json}", json);


            logger.LogInformation("Parsing payload.");
            var runStateChangedEventPayload = JsonDocument.Parse(json);
            var runState = runStateChangedEventPayload.RootElement.GetProperty("resource").GetProperty("run").GetProperty("state").GetString();
            var runUrl = runStateChangedEventPayload.RootElement.GetProperty("resource").GetProperty("runUrl").GetString();
            var runUri = new Uri(runUrl);
            
            if (runState == "completed")
            {
                await runProcessor.ProcessRunAsync(runUri);
            }
            else
            {
                logger.LogInformation($"Skipping incomplete run: {runUri}");
                return;
            }
        }
    }
}
