using GitHubIssues.Reports;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace FindNewItems
{
    public partial class Program
    {
#if !DEBUG
        [FunctionName("FindNewGitHubIssuesAndPRs_7am")]
        public static void FindNewGitHubIssuesAndPRs_AM([TimerTrigger("0 30 14 * * *")]TimerInfo myTimer, ILogger log)
        {
            // Run at 14:30 UTC
            new FindNewGitHubIssuesAndPRs(log).Execute();
        }

        [FunctionName("FindNewGitHubIssuesAndPRs_2pm")]
        public static void FindNewGitHubIssuesAndPRs_PM([TimerTrigger("0 30 21 * * *")]TimerInfo myTimer, ILogger log)
        {
            // Run at 21:30 UTC
            new FindNewGitHubIssuesAndPRs(log).Execute();
        }

        [FunctionName("FindIssuesInPastDueMilestones")]
        public static void FindIssuesInPastDueMilestones([TimerTrigger("0 0 15 * * WED")]TimerInfo myTimer, ILogger log)
        {
            // Run at 15:00 UTC on Wednesday morning.
            new FindIssuesInPastDueMilestones(log).Execute();
        }

        [FunctionName("FindIssuesInBacklogMilestones")]
        public static void FindIssuesInBacklogMilestones([TimerTrigger("0 30 15 1 * *")]TimerInfo myTimer, ILogger log)
        {
            // Run at 15:30 UTC on the first of the month.
            new FindIssuesInBacklogMilestones(log).Execute();
        }

        [FunctionName("FindCustomerReportedIssuesInInvalidState")]
        public static void FindCustomerReportedIssuesInInvalidState([TimerTrigger("0 0 16 * * MON")]TimerInfo myTimer, ILogger log)
        {
            // Run at 16:00 UTC every Monday.
            new FindCustomerRelatedIssuesInvalidState(log).Execute();
        }

        [FunctionName("FindPRsOlderThan3Months")]
        public static void FindPRsOlderThan3Months([TimerTrigger("0 30 15 * * MON")]TimerInfo myTimer, ILogger log)
        {
            // Runs at 7:30AM on Monday morning.
            new FindStalePRs(log).Execute();
        }
#endif
    }
}
