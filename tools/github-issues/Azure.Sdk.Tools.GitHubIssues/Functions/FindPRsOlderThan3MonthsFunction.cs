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
    public class FindPRsOlderThan3MonthsFunction
    {
        public FindPRsOlderThan3MonthsFunction(FindStalePRs report)
        {
            this.report = report;
        }

        private FindStalePRs report;

        [FunctionName("FindPRsOlderThan3MonthsFunction")]
        public async Task Run([TimerTrigger("0 30 15 * * MON")]TimerInfo timer)
        {
            await report.ExecuteAsync();
        }
    }
}
