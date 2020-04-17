using GitHubIssues.Reports;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace FindNewItems
{
    public partial class Program
    {
#if DEBUG
        static private volatile bool hasExecuted = false;
        [FunctionName("DebugFunction")]
        public static void DebugThis([TimerTrigger("* * * * * *")]TimerInfo myTimer, ILogger log)
        {
            if (!hasExecuted)
            {
                new FindIssuesInBacklogMilestones(log).Execute();
                hasExecuted = true;
            }
            else
            {
                log.LogError("The function has executed already!");
            }
        }
#endif
    }
}
