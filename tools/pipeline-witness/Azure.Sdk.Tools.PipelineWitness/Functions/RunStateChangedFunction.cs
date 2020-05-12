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
        public async Task Run([EventHubTrigger("run-state-changed", Connection = "PipelineWitnessEventHubConnectionString")] EventData @event, ILogger log)
        {
            string messageBody = Encoding.UTF8.GetString(@event.Body.Array, @event.Body.Offset, @event.Body.Count);
            //var message = JsonDocument.Parse(messageBody);
            //var content = message.RootElement.GetProperty("content");
            //var resource = content.GetProperty("resource");
            //var run = resource.GetProperty("run");
            //var pipeline = run.GetProperty("pipeline");

            //var notificationId = content.GetProperty("id").GetString();
            //var runId = run.GetProperty("id").GetInt32();
            //var runUrl = run.GetProperty("url").GetString();
            //var runResult = run.GetProperty("result").GetString();
            //var runState = run.GetProperty("state").GetString();
            //var pipelineId = pipeline.GetProperty("id").GetInt32();
            //var pipelineName = pipeline.GetProperty("name").GetString();
            //var pipelineFolder = pipeline.GetProperty("folder").GetString();

            //log.LogInformation(
            //    "Run state changed for pipelineName:{pipelineName} runId:{runId} runState:{runState} runResult:{runResult} runUrl:{runUrl}",
            //    pipelineName,
            //    runId,
            //    runState,
            //    runResult,
            //    runUrl
            //    );
        }
    }
}
