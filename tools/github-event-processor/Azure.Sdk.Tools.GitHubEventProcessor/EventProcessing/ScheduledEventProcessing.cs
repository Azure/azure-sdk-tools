using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class ScheduledEventProcessing
    {
        // Each repository has rate limit for actions of 1000/hour. Because scheduled tasks have the potential
        // to cause a large number of updates, put an initial limit on the number of updates a scheduled task
        // can make.
        private static int ScheduledTaskUpdateLimit = 100;
        // There's a secondary limit to the search API which is 1000 items. The default page size for the search
        // is 100 items (which is also the max per page)
        private static int MaxSearchResults = 1000;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        /// <param name="cronTaskToRun">String, the scheduled even</param>
        public static async Task ProcessScheduledEvent(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload, string cronTaskToRun)
        {
            switch (cronTaskToRun)
            {
                case RulesConstants.CloseAddressedIssues:
                    {
                        await CloseAddressedIssues(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                case RulesConstants.CloseStaleIssues:
                    {
                        await CloseStaleIssues(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                case RulesConstants.CloseStalePullRequests:
                    {
                        await CloseStalePullRequests(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                case RulesConstants.IdentifyStaleIssues:
                    {
                        await IdentifyStaleIssues(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                case RulesConstants.IdentifyStalePullRequests:
                    {
                        await IdentifyStalePullRequests(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                case RulesConstants.LockClosedIssues:
                    {
                        await LockClosedIssues(gitHubEventClient, scheduledEventPayload);
                        break;
                    }
                default:
                    {
                        Console.WriteLine($"{cronTaskToRun} is not valid Scheduled Event rule. Please ensure the scheduled event yml is correctly passing in the correct rules constant.");
                        break;
                    }
            }
            // The second argument is IssueOrPullRequestNumber which isn't applicable to scheduled events (cron tasks)
            // since they're not going to be changing a single IssueUpdate like rules processing does.
            await gitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is open
        ///     Issue has label "issue-addressed"
        ///     Issue was last updated more than 7 days ago
        /// Resulting Action:
        ///     Close the issue
        ///     Create a comment "Hi @${issueAuthor}, since you haven’t asked that we `/unresolve` the issue, we’ll close this out. If you believe further discussion is needed, please add a comment `/unresolve` to reopen the issue."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task CloseAddressedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseAddressedIssues))
            {

                List<string> includeLabels = new List<string>
                {
                    LabelConstants.IssueAddressed
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 null,
                                                                 includeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1; request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        issueUpdate.State = ItemState.Closed;
                        issueUpdate.StateReason = ItemStateReason.Completed;
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id,
                                                               issue.Number,
                                                               issueUpdate);
                        string comment = $"Hi @{issue.User.Login}, since you haven’t asked that we `/unresolve` the issue, we’ll close this out. If you believe further discussion is needed, please add a comment `/unresolve` to reopen the issue.";
                        gitHubEventClient.CreateComment(scheduledEventPayload.Repository.Id,
                                                        issue.Number,
                                                        comment);
                        // There are 2 updates per result
                        numUpdates += 2;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount ||
                        result.Items.Count < request.PerPage ||
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task CloseStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseStaleIssues))
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
                                                                 14, // more than 14 days since last update
                                                                 null,
                                                                 includeLabels);

                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1;request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        issueUpdate.State = ItemState.Closed;
                        issueUpdate.StateReason = ItemStateReason.NotPlanned;
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id, 
                                                               issue.Number, 
                                                               issueUpdate);
                        numUpdates++;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount ||
                        result.Items.Count < request.PerPage ||
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Pull request is open
        ///     Pull request has "no-recent-activity" label
        ///     Pull request was last modified more than 7 days ago
        /// Resulting Action:
        ///     Close the pull request
        ///     Create a comment "Hi @${issueAuthor}.  Thank you for your contribution.  Since there hasn't been recent engagement, we're going to close this out.  Feel free to respond with a comment containing `/reopen` if you'd like to continue working on these changes.  Please be sure to use the command to reopen or remove the `no-recent-activity` label; otherwise, this is likely to be closed again with the next cleanup pass."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task CloseStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.CloseStalePullRequests))
            {

                List<string> includeLabels = new List<string>
                {
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 null,
                                                                 includeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1; request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        issueUpdate.State = ItemState.Closed;
                        issueUpdate.StateReason = ItemStateReason.NotPlanned;
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id,
                                                               issue.Number,
                                                               issueUpdate);
                        string comment = $"Hi @{issue.User.Login}.  Thank you for your contribution.  Since there hasn't been recent engagement, we're going to close this out.  Feel free to respond with a comment containing `/reopen` if you'd like to continue working on these changes.  Please be sure to use the command to reopen or remove the `no-recent-activity` label; otherwise, this is likely to be closed again with the next cleanup pass.";
                        gitHubEventClient.CreateComment(scheduledEventPayload.Repository.Id,
                                                        issue.Number,
                                                        comment);
                        // There are 2 updates per result
                        numUpdates += 2;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount ||
                        result.Items.Count < request.PerPage ||
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Trigger: weekly, Friday at 5am
        /// Query Criteria
        ///     Pull request is open
        ///     Pull request does NOT have "no-recent-activity" label
        ///     Pull request was last updated more than 60 days ago
        /// Resulting Action: 
        ///     Add "no-recent-activity" label
        ///     Create a comment "Hi @${issueAuthor}.  Thank you for your interest in helping to improve the Azure SDK experience and for your contribution.  We've noticed that there hasn't been recent engagement on this pull request.  If this is still an active work stream, please let us know by pushing some changes or leaving a comment.  Otherwise, we'll close this out in 7 days."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task IdentifyStalePullRequests(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IdentifyStalePullRequests))
            {

                List<string> excludeLabels = new List<string>
                {
                    LabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 60, // more than 60 days since last update
                                                                 null,
                                                                 null,
                                                                 excludeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1; request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        issueUpdate.AddLabel(LabelConstants.NoRecentActivity);
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id,
                                                               issue.Number,
                                                               issueUpdate);
                        string comment = $"Hi @{issue.User.Login}.  Thank you for your interest in helping to improve the Azure SDK experience and for your contribution.  We've noticed that there hasn't been recent engagement on this pull request.  If this is still an active work stream, please let us know by pushing some changes or leaving a comment.  Otherwise, we'll close this out in 7 days.";
                        gitHubEventClient.CreateComment(scheduledEventPayload.Repository.Id,
                                                        issue.Number,
                                                        comment);
                        // There are 2 updates per result
                        numUpdates +=2;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount ||
                        result.Items.Count < request.PerPage ||
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is open
        ///     Issue has "needs-author-feedback" label
        ///     Issue does NOT have "no-recent-activity" label
        ///     Issue was last updated more than 7 days ago
        /// Resulting Action: 
        ///     Add "no-recent-activity" label
        ///     Create a comment: "Hi @{issueAuthor}, we're sending this friendly reminder because we haven't heard back from you in **7 days**. We need more information about this issue to help address it. Please be sure to give us your input. If we don't hear back from you within **14 days** of this comment the issue will be automatically closed. Thank you!"
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task IdentifyStaleIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IdentifyStaleIssues))
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
                                                                 7, // more than 7 days since the last update
                                                                 null,
                                                                 includeLabels,
                                                                 excludeLabels);

                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1; request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        issueUpdate.AddLabel(LabelConstants.NoRecentActivity);
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id,
                                                               issue.Number,
                                                               issueUpdate);
                        string comment = $"Hi @{issue.User.Login}, we're sending this friendly reminder because we haven't heard back from you in **7 days**. We need more information about this issue to help address it. Please be sure to give us your input. If we don't hear back from you within **14 days** of this comment the issue will be automatically closed. Thank you!";
                        gitHubEventClient.CreateComment(scheduledEventPayload.Repository.Id,
                                                        issue.Number,
                                                        comment);
                        // There are 2 updates per result
                        numUpdates += 2;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount ||
                        result.Items.Count < request.PerPage ||
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Trigger: every 6 hours
        /// Query Criteria
        ///     Issue is closed
        ///     Issue was last updated more than 90 days ago
        ///     Issue is unlocked
        /// Resulting Action:
        ///     Lock issue conversations
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task LockClosedIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.LockClosedIssues))
            {

                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(
                    scheduledEventPayload.Repository.Owner.Login,
                    scheduledEventPayload.Repository.Name,
                    IssueTypeQualifier.Issue,
                    ItemState.Closed,
                    90, // more than 90 days
                    new List<IssueIsQualifier> { IssueIsQualifier.Unlocked });

                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = MaxSearchResults / request.PerPage;
                for (request.Page = 1; request.Page <= maximumPage; request.Page++)
                {
                    SearchIssuesResult result = await gitHubEventClient.QueryIssues(request);
                    int iCounter = 0;
                    while (
                        // Process every item in the page returned
                        iCounter < result.Items.Count &&
                        // unless the update limit has been hit
                        numUpdates < ScheduledTaskUpdateLimit
                        )
                    {
                        Issue issue = result.Items[iCounter++];
                        gitHubEventClient.LockIssue(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
                        numUpdates++;
                    }

                    // The number of items in the query isn't known until the query is run.
                    // If the number of items in the result equals the total number of items matching the query then
                    // all the items have been processed.
                    // OR 
                    // If the number of items in the result is less than the number of items requested per page then
                    // the last page of results has been processed which was not a full page
                    // OR
                    // The number of updates has hit the limit for a scheduled task
                    if (result.Items.Count == result.TotalCount || 
                        result.Items.Count < request.PerPage || 
                        numUpdates >= ScheduledTaskUpdateLimit)
                    {
                        break;
                    }
                }
            }
        }
    }
}
