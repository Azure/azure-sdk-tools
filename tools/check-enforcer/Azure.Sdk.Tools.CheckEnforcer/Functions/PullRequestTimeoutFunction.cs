using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.CheckEnforcer.Functions
{
    public class PullRequestTimeoutFunction
    {
        [FunctionName("PullRequestTimeoutFunction")]
        public async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
