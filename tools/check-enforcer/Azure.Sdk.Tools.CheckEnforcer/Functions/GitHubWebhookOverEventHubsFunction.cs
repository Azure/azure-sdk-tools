using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.CheckEnforcer.Functions
{
    public class GitHubWebhookOverEventHubsFunction
    {
        public GitHubWebhookOverEventHubsFunction(GitHubWebhookProcessor processor)
        {
            this.processor = processor;
        }

        private GitHubWebhookProcessor processor;

        [FunctionName("webhook-eventhubs")]
        public async Task Run([EventHubTrigger("github-webhooks", Connection = "CheckEnforcerEventHubConnectionString")] EventData eventData, ILogger log, CancellationToken cancellationToken)
        {
            var message = GetMessage(eventData);
            var eventName = GetEventName(message);

            var encodedContent = message.RootElement.GetProperty("content").ToString();
            var contentBytes = Convert.FromBase64String(encodedContent);
            var json = Encoding.UTF8.GetString(contentBytes);

            await processor.ProcessWebhookAsync(eventName, json, log, cancellationToken);
        }

        private string GetEventName(JsonDocument message)
        {
            return message
                .RootElement
                .GetProperty("headers")
                .GetProperty("X-GitHub-Event")
                .EnumerateArray()
                .Single()
                .ToString();
        }

        private JsonDocument GetMessage(EventData eventData)
        {
            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
            var message = JsonDocument.Parse(messageBody);
            return message;
        }
    }
}