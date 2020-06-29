using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PullRequestLabeler
{
    public class EvaluatePullRequestFunction
    {
        [FunctionName("evaluate-pull-request")]
        public async Task Run([EventHubTrigger("webhooks", Connection = "PullRequestLabelerEventHubConnectionString")] EventData @event, ILogger log)
        {
        }
    }
}
