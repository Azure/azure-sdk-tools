using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Functions
{
    public class RunStateChangedFunction
    {
        [FunctionName("RunStateChanged")]
        public async Task Run([EventHubTrigger("run-state-changed", Connection = "PipelineWitnessEventHubConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var message = JsonDocument.Parse(messageBody);
                    var content = message.RootElement.GetProperty("content");
                    var resource = content.GetProperty("resource");
                    var run = resource.GetProperty("run");
                    var pipeline = run.GetProperty("pipeline");

                    var notificationId = content.GetProperty("id").GetString();
                    var runId = run.GetProperty("id").GetInt32();
                    var runUrl = run.GetProperty("url").GetString();
                    var runResult = run.GetProperty("result").GetString();
                    var runState = run.GetProperty("state").GetString();
                    var pipelineId = pipeline.GetProperty("id").GetInt32();
                    var pipelineName = pipeline.GetProperty("name").GetString();
                    var pipelineFolder = pipeline.GetProperty("folder").GetString();

                    log.LogInformation(
                        "Run state changed for pipelineName:{pipelineName} runId:{runId} runState:{runState} runResult:{runResult} runUrl:{runUrl}",
                        pipelineName,
                        runId,
                        runState,
                        runResult,
                        runUrl
                        );

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
