using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PullRequestLabeler.Services.GitHubIntegration;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Octokit.Internal;

namespace Azure.Sdk.Tools.PullRequestLabeler
{
    public class EvaluatePullRequestFunction
    {
        public EvaluatePullRequestFunction(ILogger<EvaluatePullRequestFunction> logger, IGitHubIntegrationService gitHubIntegrationService)
        {
            this.logger = logger;
            this.gitHubIntegrationService = gitHubIntegrationService;
        }

        private ILogger logger;
        private IGitHubIntegrationService gitHubIntegrationService;

        [FunctionName("evaluate-pull-request")]
        public async Task Run([EventHubTrigger("webhooks", Connection = "PullRequestLabelerEventHubConnectionString", ConsumerGroup = "debug")] EventData @event, ILogger log)
        {
            string messageBody = Encoding.UTF8.GetString(@event.Body.Array, @event.Body.Offset, @event.Body.Count);
            var message = JsonDocument.Parse(messageBody);
            var base64EncodedJson = message.RootElement.GetProperty("content").ToString();
            var jsonBytes = Convert.FromBase64String(base64EncodedJson);
            var json = Encoding.UTF8.GetString(jsonBytes);

            var client = await gitHubIntegrationService.GetGitHubInstallationClientAsync(0);
        }
    }
}
