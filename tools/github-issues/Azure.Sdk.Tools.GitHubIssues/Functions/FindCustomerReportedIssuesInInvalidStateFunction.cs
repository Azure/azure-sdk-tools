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
    public class FindCustomerReportedIssuesInInvalidStateFunction
    {
        public FindCustomerReportedIssuesInInvalidStateFunction(FindCustomerRelatedIssuesInvalidState report)
        {
            this.report = report;
        }

        private FindCustomerRelatedIssuesInvalidState report;

        [FunctionName("FindCustomerReportedIssuesInInvalidStateFunction")]
        public async Task Run([TimerTrigger("0 0 16 * * MON")]TimerInfo timer)
        {
            await report.ExecuteAsync();
        }
    }
}
