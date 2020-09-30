using Azure.Sdk.Tools.GitHubIssues.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Functions
{
    public class FindIssuesInBacklogMilestonesFunction
    {
        public FindIssuesInBacklogMilestonesFunction(FindIssuesInBacklogMilestones report)
        {
            this.report = report;
        }

        private FindIssuesInBacklogMilestones report;

        [FunctionName("FindIssuesInBacklogMilestonesFunction")]
        public async Task Run([TimerTrigger("0 30 15 1 * *")]TimerInfo timer)
        {
            await report.ExecuteAsync();
        }
    }
}
