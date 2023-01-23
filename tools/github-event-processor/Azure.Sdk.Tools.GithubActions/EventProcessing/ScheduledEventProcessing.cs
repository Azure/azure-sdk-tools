using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    internal class ScheduledEventProcessing
    {
        internal static async Task ProcessScheduledEvent(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            // 6 hour cron tasks
            // await IdentifyStaleIssues(_gitHubClient, scheduledEventPayload);
            // await CloseStalePullRequests(_gitHubClient, scheduledEventPayload);
            // await CloseAddressedIssues(_gitHubClient, scheduledEventPayload);
            await LockClosedIssues(gitHubEventClient, scheduledEventPayload);
        }

        /// <summary>
        /// Trigger: Daily 1am
        /// Query Criteria
        ///     Issue is open
        ///     Issue has "needs-author-feedback" label
        ///     Issue has "no-recent-activity" label
        ///     Issue was last modified more than 14 days ago
        /// Resulting Action: 
        ///     Close the issue
        /// </summary>
        /// <param name="gitHubEventClient"></param>
        /// <param name="scheduledEventPayload"></param>
        /// <returns></returns>
        internal static async Task CloseStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NeedsAuthorFeedback,
                LabelConstants.NoRecentActivity
            };
            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             14, // more than 14 days old
                                                             null,
                                                             includeLabels);
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
            foreach (var issue in result.Items)
            {

            }

        }

        internal static async Task IdentifyStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NeedsAuthorFeedback
            };
            List<string> excludeLabels = new List<string>
            {
                LabelConstants.NoRecentActivity
            };
            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             14, // more than 14 days old
                                                             null,
                                                             includeLabels,
                                                             excludeLabels);
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

            Console.WriteLine(result.TotalCount);
        }
        internal static async Task CloseStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NoRecentActivity
            };
            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.PullRequest,
                                                             ItemState.Open,
                                                             7,
                                                             null,
                                                             includeLabels);
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

            Console.WriteLine(result.TotalCount);
        }

        internal static async Task CloseAddressedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.IssueAddressed
            };
            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             7,
                                                             null,
                                                             includeLabels);
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);

            Console.WriteLine(result.TotalCount);
        }

        internal static async Task LockClosedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            //var result = await gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
            //                                                 scheduledEventPayload.Repository.Name,
            //                                                 IssueTypeQualifier.Issue,
            //                                                 ItemState.Closed,
            //                                                 0,
            //                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });

            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest("JimSuplizio",
                                                             "azure-sdk-tools",
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Closed,
                                                             0,
                                                             new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });
            // Grab the first 100 issues
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
            Console.WriteLine(result.TotalCount);
            for (int i = 0; i < result.TotalCount;i+=100)
            {
                //foreach (Issue issue in result.Items)
                //{
                //    Console.WriteLine(issue.Number);
                //    //await gitHubClient.Issue.LockUnlock.Lock(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
                //}
                request.Page++;
                if (request.Page < 10)
                {
                    Console.WriteLine($"grabbing next page, {request.Page}");
                    result = await gitHubEventClient.QueryIssues(request);
                }
                else
                {
                    break;
                }
            }
            Console.WriteLine("done");
        }

        internal static async Task TestSearchForLockedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Closed,
                                                             0,
                                                             new List<IssueIsQualifier> { IssueIsQualifier.Locked });
            SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
            Console.WriteLine(result.TotalCount);
        }
    }
}
