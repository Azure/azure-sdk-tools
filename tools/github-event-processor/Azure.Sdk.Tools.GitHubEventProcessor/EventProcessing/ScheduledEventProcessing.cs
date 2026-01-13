using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
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
        /// <summary>
        /// Scheduled events, unlike regular actions, need to have which scheduled event to run, passed in otherwise, it require
        /// looking at the cron schedule to determine the event. Doing it this way means if the schedule changes only the cron
        /// in the yml file needs to change rather than having to coordinate actions processing changes with the yml update. 
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        /// <param name="cronTaskToRun">String, the scheduled event</param>
        public static async Task ProcessScheduledEvent(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload, string cronTaskToRun, McpIssueProcessing mcpProcessor)
        {
            // Scheduled events can make multiple calls to SearchIssues due to pagination. Any call to SearchIssues can
            // run into a SecondaryRateLimitExceededException, regardless of the page, and there could be pending updates
            // from previous pages that were processed. Because of this, the call to process pending updates should be made
            // regardless of whether there is an exception or not. If there aren't any updates then the call is effectively
            // a no-op so it's good either way.
            try
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
                    case RulesConstants.EnforceMaxLifeOfIssues:
                        {
                            await EnforceMaxLifeOfIssues(gitHubEventClient, scheduledEventPayload);
                            break;
                        }
                    case RulesConstants.BackfillMcpHistoricalIssues:
                        {
                            await BackfillMcpHistoricalIssues(gitHubEventClient, scheduledEventPayload, mcpProcessor);
                            break;
                        }
                    // case RulesConstants.RefreshMcpLabels:
                    //     {
                    //         await RefreshMcpLabels(gitHubEventClient, scheduledEventPayload);
                    //         break;
                    //     }
                    default:
                        {
                            Console.WriteLine($"{cronTaskToRun} is not valid Scheduled Event rule. Please ensure the scheduled event yml is correctly passing in the correct rules constant.");
                            break;
                        }
                }
            }
            finally
            {
                // The second argument is IssueOrPullRequestNumber which isn't applicable to scheduled events (cron tasks)
                // since they're not going to be changing a single IssueUpdate like rules processing does.
                await gitHubEventClient.ProcessPendingScheduledUpdates();
            }
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                List<string> includeLabels = new List<string>
                {
                    TriageLabelConstants.IssueAddressed
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                                                                 includeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
                        // This rule only sets the state
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                List<string> includeLabels = new List<string>
                {
                    TriageLabelConstants.NeedsAuthorFeedback,
                    TriageLabelConstants.NoRecentActivity
                };

                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 14, // more than 14 days since last update
                                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                                                                 includeLabels);

                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
                        // This rule only sets the state
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                List<string> includeLabels = new List<string>
                {
                    TriageLabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days old
                                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                                                                 includeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
                        // This rule only sets the state
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                List<string> excludeLabels = new List<string>
                {
                    TriageLabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.PullRequest,
                                                                 ItemState.Open,
                                                                 60, // more than 60 days since last update
                                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                                                                 null,
                                                                 excludeLabels);
                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
                
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
                        // This rule needs to the full IssueUpdate as it's adding a label
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false, false);
                        issueUpdate.AddLabel(TriageLabelConstants.NoRecentActivity);
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                List<string> includeLabels = new List<string>
                {
                    TriageLabelConstants.NeedsAuthorFeedback
                };
                List<string> excludeLabels = new List<string>
                {
                    TriageLabelConstants.NoRecentActivity
                };
                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(scheduledEventPayload.Repository.Owner.Login,
                                                                 scheduledEventPayload.Repository.Name,
                                                                 IssueTypeQualifier.Issue,
                                                                 ItemState.Open,
                                                                 7, // more than 7 days since the last update
                                                                 new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                                                                 includeLabels,
                                                                 excludeLabels);

                // Need to stop updating when the we hit the limit but, until then, after exhausting every
                // issue in the page returned, the query needs to be rerun to get the next page
                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
                        // This rule needs to the full IssueUpdate as it's adding a label
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false, false);
                        issueUpdate.AddLabel(TriageLabelConstants.NoRecentActivity);
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
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

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
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
        /// <summary>
        /// Trigger: Weekly, Monday at 10am
        /// Query Criteria
        ///     Issue is open
        ///     Issue was last updated more than 30 days ago
        ///     Issue is unlocked
        ///     Issue was created more than 2 years ago
        /// Resulting Action:
        ///     Close the issue
        ///     Add a comment: Hi @{issue.User.Login}, we deeply appreciate your input into this project. Regrettably, 
        ///                    this issue has remained unresolved for over 2 years and inactive for 30 days, leading us
        ///                    to the decision to close it. We've implemented this policy to maintain the relevance of
        ///                    our issue queue and facilitate easier navigation for new contributors. If you still believe
        ///                    this topic requires attention, please feel free to create a new issue, referencing this
        ///                    one. Thank you for your understanding and ongoing support.
        ///     Lock the issue
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task EnforceMaxLifeOfIssues(GitHubEventClient gitHubEventClient, ScheduledEventGitHubPayload scheduledEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.EnforceMaxLifeOfIssues))
            {
                int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();

                /// An issue with the auto-close-exempt label will exempt the issue from being closed by this rule
                List<string> excludeLabels = new List<string>
                {
                    TriageLabelConstants.AutoCloseExempt
                };

                SearchIssuesRequest request = gitHubEventClient.CreateSearchRequest(
                    scheduledEventPayload.Repository.Owner.Login,
                    scheduledEventPayload.Repository.Name,
                    IssueTypeQualifier.Issue,
                    ItemState.Open,
                    30, // more than 30 days
                    new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                    null,
                    excludeLabels,
                    365*2 // Created date > 2 years
                    );

                int numUpdates = 0;
                // In theory, maximumPage will be 10 since there's 100 results per-page returned by default but
                // this ensures that if we opt to change the page size
                int maximumPage = RateLimitConstants.SearchIssuesRateLimit / request.PerPage;
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
                        // This rule only sets the state
                        IssueUpdate issueUpdate = gitHubEventClient.GetIssueUpdate(issue, false);
                        // Close the issue
                        issueUpdate.State = ItemState.Closed;
                        issueUpdate.StateReason = ItemStateReason.NotPlanned;
                        gitHubEventClient.AddToIssueUpdateList(scheduledEventPayload.Repository.Id,
                                                               issue.Number,
                                                               issueUpdate);
                        // Add a comment
                        string comment = $"Hi @{issue.User.Login}, we deeply appreciate your input into this project. Regrettably, this issue has remained unresolved for over 2 years and inactive for 30 days, leading us to the decision to close it. We've implemented this policy to maintain the relevance of our issue queue and facilitate easier navigation for new contributors. If you still believe this topic requires attention, please feel free to create a new issue, referencing this one. Thank you for your understanding and ongoing support.";
                        gitHubEventClient.CreateComment(scheduledEventPayload.Repository.Id,
                                                        issue.Number,
                                                        comment);
                        // Lock the issue
                        gitHubEventClient.LockIssue(scheduledEventPayload.Repository.Id, issue.Number, LockReason.Resolved);
                        // Close, Comment and Lock = 3 updates per issue
                        numUpdates += 3;
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
        /// 
        /// Trigger: Manual execution (workflow_dispatch)
        /// Query Criteria:
        ///     Repository is microsoft/mcp
        ///     Issue is open
        ///     Issue has no labels OR only has "needs-triage" label
        /// Resulting Action:
        ///     Run InitialIssueTriage logic on each issue (predict labels, assign owners, add triage labels)
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        public static async Task BackfillMcpHistoricalIssues(GitHubEventClient gitHubEventClient, 
        ScheduledEventGitHubPayload scheduledEventPayload, McpIssueProcessing mcpProcessor)
        {
            if (!scheduledEventPayload.Repository.Owner.Login.Equals("microsoft", StringComparison.OrdinalIgnoreCase) ||
                !scheduledEventPayload.Repository.Name.Equals("mcp", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int ScheduledTaskUpdateLimit = await gitHubEventClient.ComputeScheduledTaskUpdateLimit();
            
            SearchIssuesRequest requestNoLabels = gitHubEventClient.CreateSearchRequest(
                scheduledEventPayload.Repository.Owner.Login,
                scheduledEventPayload.Repository.Name,
                IssueTypeQualifier.Issue,
                ItemState.Open,
                0,
                new List<IssueIsQualifier> { IssueIsQualifier.Unlocked },
                new List<string>()
            );

            int numUpdates = 0;
            int totalIssuesProcessed = 0;

            var result = await gitHubEventClient.QueryIssues(requestNoLabels);
            var allIssues = result.Items.ToList();
            var serializer = new SimpleJsonSerializer();

            foreach (var issue in allIssues)
            {
                if (numUpdates >= ScheduledTaskUpdateLimit)
                {
                    break;
                }

                var payloadJson = $@"{{
                    ""action"": ""{ActionConstants.Opened}"",
                    ""issue"": {serializer.Serialize(issue)},
                    ""repository"": {serializer.Serialize(scheduledEventPayload.Repository)},
                    ""sender"": {serializer.Serialize(issue.User)}
                }}";
                
                var issuePayload = serializer.Deserialize<IssueEventGitHubPayload>(payloadJson);
                await IssueProcessing.ProcessIssueEvent(gitHubEventClient, issuePayload, mcpProcessor);
                
                numUpdates++;
                totalIssuesProcessed++;
            }

            Console.WriteLine($"MCP historical issue backfill complete. Processed {totalIssuesProcessed} issues.");
        }

        /// <summary>
        /// Trigger: Daily at 2am UTC
        /// Query Criteria:
        ///     Repository is microsoft/mcp
        /// Resulting Action:
        ///     Fetch all labels from microsoft/mcp repository
        ///     Filter service labels (yellow: ffeb77) and tool labels (red: e99695)
        ///     Upload label data to Azure Blob Storage for AI-powered issue triage
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="scheduledEventPayload">ScheduledEventGitHubPayload deserialized from the json event payload</param>
        // public static async Task RefreshMcpLabels(GitHubEventClient gitHubEventClient,
        //     ScheduledEventGitHubPayload scheduledEventPayload)
        // {
        //     // Only run for microsoft/mcp repository
        //     if (!scheduledEventPayload.Repository.Owner.Login.Equals("microsoft", StringComparison.OrdinalIgnoreCase) ||
        //         !scheduledEventPayload.Repository.Name.Equals("mcp", StringComparison.OrdinalIgnoreCase))
        //     {
        //         Console.WriteLine($"Skipping label refresh for repository {scheduledEventPayload.Repository.Owner.Login}/{scheduledEventPayload.Repository.Name}");
        //         return;
        //     }

        //     Console.WriteLine("Starting MCP label refresh...");

        //     try
        //     {
        //         // Get storage account name from environment variable
        //         var storageAccountName = Environment.GetEnvironmentVariable("MCP_ISSUE_STORAGE_NAME");
        //         if (string.IsNullOrEmpty(storageAccountName))
        //         {
        //             throw new InvalidOperationException("MCP_ISSUE_STORAGE_NAME environment variable is not set");
        //         }

        //         // Fetch labels from the repository using GitHubEventClient's connection
        //         var labels = await gitHubEventClient.GetLabels(
        //             scheduledEventPayload.Repository.Owner.Login,
        //             scheduledEventPayload.Repository.Name);

        //         Console.WriteLine($"Fetched {labels.Count} total labels from {scheduledEventPayload.Repository.Name}");

        //         // Filter labels by color (service labels: ffeb77 yellow, tool labels: e99695 red)
        //         var filteredLabels = labels
        //             .Where(label =>
        //                 string.Equals(label.Color, "ffeb77", StringComparison.OrdinalIgnoreCase) ||
        //                 string.Equals(label.Color, "e99695", StringComparison.OrdinalIgnoreCase))
        //             .Select(label => new
        //             {
        //                 Name = label.Name,
        //                 Color = label.Color
        //             })
        //             .ToList();

        //         Console.WriteLine($"Filtered to {filteredLabels.Count} service/tool labels");

        //         // Upload to Azure Blob Storage
        //         var blobServiceClient = new BlobServiceClient(
        //             new Uri($"https://{storageAccountName}.blob.core.windows.net/"),
        //             new DefaultAzureCredential());

        //         var containerClient = blobServiceClient.GetBlobContainerClient("labels");
        //         await containerClient.CreateIfNotExistsAsync();

        //         var blobClient = containerClient.GetBlobClient("mcp");
        //         var labelsJson = JsonSerializer.Serialize(filteredLabels);

        //         var blobHttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
        //         {
        //             ContentType = "application/json"
        //         };

        //         using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(labelsJson)))
        //         {
        //             await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        //             {
        //                 HttpHeaders = blobHttpHeaders,
        //                 Overwrite = true
        //             });
        //         }

        //         Console.WriteLine($"✅ Successfully uploaded MCP labels to blob storage: {storageAccountName}/labels/mcp");
        //         Console.WriteLine("Labels are now up-to-date for AI-powered issue triage");
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"❌ Error refreshing MCP labels: {ex.Message}");
        //         throw;
        //     }
        // }
    }
}
