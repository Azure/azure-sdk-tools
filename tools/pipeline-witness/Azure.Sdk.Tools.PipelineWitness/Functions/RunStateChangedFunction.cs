using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Records;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Functions
{
    public class RunStateChangedFunction
    {
        public RunStateChangedFunction(IRecordStore recordStore)
        {
            this.recordStore = recordStore;
        }

        private IRecordStore recordStore;

        [FunctionName("RunStateChanged")]
        public async Task Run([EventHubTrigger("run-state-changed", Connection = "PipelineWitnessEventHubConnectionString", ConsumerGroup = "localdebugging")] EventData @event, ILogger log)
        {
            string messageBody = Encoding.UTF8.GetString(@event.Body.Array, @event.Body.Offset, @event.Body.Count);
            var message = JsonDocument.Parse(messageBody);
            var encodedContent = message.RootElement.GetProperty("content").ToString();
            var contentBytes = Convert.FromBase64String(encodedContent);
            var content = Encoding.UTF8.GetString(contentBytes);
            await recordStore.PutRecordAsync<RunStateChangedEventRecord>(content);
        }
    }
}
