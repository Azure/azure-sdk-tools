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
        internal static async Task ProcessScheduledEvent(GitHubEventClient gitHubEventClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            ScheduledEventGitHubPayload scheduledEventPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
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
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             14, // more than 14 days old
                                                             null,
                                                             includeLabels);

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
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             14, // more than 14 days old
                                                             null,
                                                             includeLabels,
                                                             excludeLabels);
            Console.WriteLine(result.TotalCount);
        }
        internal static async Task CloseStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NoRecentActivity
            };
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.PullRequest,
                                                             ItemState.Open,
                                                             7,
                                                             null,
                                                             includeLabels);
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task CloseAddressedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.IssueAddressed
            };
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Open,
                                                             7,
                                                             null,
                                                             includeLabels);
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task LockClosedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Closed,
                                                             0,
                                                             new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });

            foreach (Issue issue in result.Items)
            {
                //await gitHubClient.Issue.LockUnlock.Lock(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
            }
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task TestSearchForLockedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            var result = await gitHubEventClient.QueryIssues(scheduledEventPayload.Repository.Owner.Login,
                                                             scheduledEventPayload.Repository.Name,
                                                             IssueTypeQualifier.Issue,
                                                             ItemState.Closed,
                                                             0,
                                                             new List<IssueIsQualifier> { IssueIsQualifier.Locked });
            Console.WriteLine(result.TotalCount);
        }
    }
}
