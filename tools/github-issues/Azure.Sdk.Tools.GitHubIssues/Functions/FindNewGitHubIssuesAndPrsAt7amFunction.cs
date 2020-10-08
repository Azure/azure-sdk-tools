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
    public class FindNewGitHubIssuesAndPrsAt7amFunction
    {

        public FindNewGitHubIssuesAndPrsAt7amFunction(FindNewGitHubIssuesAndPRs report)
        {
            this.report = report;
        }

        private FindNewGitHubIssuesAndPRs report;

        [FunctionName("FindNewGitHubIssuesAndPRs_7am")]
        public async Task Run([TimerTrigger("0 30 14 * * *")]TimerInfo timer)
        {
            await report.ExecuteAsync();
        }
    }
}
