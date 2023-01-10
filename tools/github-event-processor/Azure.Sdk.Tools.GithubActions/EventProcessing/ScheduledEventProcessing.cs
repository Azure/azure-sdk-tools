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
        internal static async Task ProcessScheduledEvent(GitHubClient gitHubClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            ScheduledEventGitHubPayload scheduledEventPayload = serializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // 6 hour cron tasks
            // await IdentifyStaleIssues(_gitHubClient, scheduledEventPayload);
            // await CloseStalePullRequests(_gitHubClient, scheduledEventPayload);
            // await CloseAddressedIssues(_gitHubClient, scheduledEventPayload);
            await LockClosedIssues(gitHubClient, scheduledEventPayload);
        }

        internal static async Task IdentifyStaleIssues(GitHubClient gitHubClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NeedsAuthorFeedback
            };
            List<string> excludeLabels = new List<string>
            {
                LabelConstants.NoRecentActivity
            };
            var result = await SearchUtil.QueryIssues(gitHubClient,
                "Azure",
                "azure-sdk-for-net",
                IssueTypeQualifier.Issue,
                ItemState.Open,
                14,
                null,
                includeLabels,
                excludeLabels);
            Console.WriteLine(result.TotalCount);
        }
        internal static async Task CloseStalePullRequests(GitHubClient gitHubClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.NoRecentActivity
            };
            var result = await SearchUtil.QueryIssues(gitHubClient,
                "Azure",
                "azure-sdk-for-net",
                IssueTypeQualifier.PullRequest,
                ItemState.Open,
                7,
                null,
                includeLabels);
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task CloseAddressedIssues(GitHubClient gitHubClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            List<string> includeLabels = new List<string>
            {
                LabelConstants.IssueAddressed
            };
            var result = await SearchUtil.QueryIssues(gitHubClient,
                "Azure",
                "azure-sdk-for-net",
                IssueTypeQualifier.Issue,
                ItemState.Open,
                7,
                null,
                includeLabels);
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task LockClosedIssues(GitHubClient gitHubClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            var result = await SearchUtil.QueryIssues(gitHubClient,
                scheduledEventPayload.Repository.Owner.Login,
                scheduledEventPayload.Repository.Name,
                IssueTypeQualifier.Issue,
                ItemState.Closed,
                0,
                new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });

            foreach (Issue issue in result.Items)
            {
                await gitHubClient.Issue.LockUnlock.Lock(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
            }
            Console.WriteLine(result.TotalCount);
        }

        internal static async Task TestSearchForLockedIssues(GitHubClient gitHubClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            var result = await SearchUtil.QueryIssues(gitHubClient,
                "JimSuplizio",
                "azure-sdk-tools",
                IssueTypeQualifier.Issue,
                ItemState.Closed,
                0,
                new List<IssueIsQualifier> { IssueIsQualifier.Locked });
            Console.WriteLine(result.TotalCount);
        }
    }
}
