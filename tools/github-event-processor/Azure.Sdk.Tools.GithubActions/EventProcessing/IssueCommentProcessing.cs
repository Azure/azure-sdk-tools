using System;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using System.Threading.Tasks;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    // Issue Comment Processing also includes PR comments. The guidance for pull_request_comment
    // say to use the issue comment event
    // https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#pull_request_comment-use-issue_comment
    //
    // If there is a rule that requires processing on issue/issue_comment, then the logic should be in a separate
    // function inside of IssueProcessing that both will call. 
    // It's also worth noting that the Octokit's IssueCommentPayload has parity to the GitHub Payload
    // which means it can be used as-is for deserialization.
    public class IssueCommentProcessing
    {
        public static async Task ProcessIssueCommentEvent(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {

            // If the Issue Comment a PullRequest Comment
            if (issueCommentPayload.Issue.PullRequest != null)
            {
                return;
            }

            AuthorFeedback(gitHubEventClient, issueCommentPayload);
            ResetIssueActivity(gitHubEventClient, issueCommentPayload);
            ReopenIssue(gitHubEventClient, issueCommentPayload);
            await DeclineToReopenIssue(gitHubEventClient, issueCommentPayload);
            await IssueAddressedCommands(gitHubEventClient, issueCommentPayload);

            int numUpdates = await gitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
        }

        /// <summary>
        /// Author Feedback https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#author-feedback
        /// Trigger: issue comment created
        /// Conditions: Issue is open
        ///             Issue has "needs-author-feedback" label
        ///             Commenter is the original issue author
        /// Resulting Action: 
        ///     Remove "needs-author-feedback" label
        ///     Add "needs-team-attention" label
        ///     Create issue comment
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated gitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static void AuthorFeedback(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.AuthorFeedback))
            {
                if (issueCommentPayload.Action == ActionConstants.Created)
                {
                    if (issueCommentPayload.Issue.State == ItemState.Open &&
                        LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                        issueCommentPayload.Sender.Login == issueCommentPayload.Issue.User.Login)
                    {
                        var issueUpdate = gitHubEventClient.GetIssueUpdate(issueCommentPayload.Issue);
                        issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                        issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                        string issueComment =
                            @$"Hi @{issueCommentPayload.Sender.Login}. Thank you for opening this issue and giving us the opportunity 
to assist. To help our team better understand your issue and the details of your scenario please provide a 
response to the question asked above or the information requested above. This will help us more accurately 
address your issue.";
                        gitHubEventClient.CreateComment(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
                    }
                }
            }
        }
        /// <summary>
        /// Reset Issue Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-issue-activity
        /// For issue_comments, the trigger Action is created 
        /// See Common_ResetIssueActivity comments
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated gitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static void ResetIssueActivity(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (issueCommentPayload.Action == ActionConstants.Created)
            {
                IssueProcessing.Common_ResetIssueActivity(gitHubEventClient, issueCommentPayload.Action, issueCommentPayload.Issue, issueCommentPayload.Sender);
            }
        }

        /// <summary>
        /// Reopen Issue https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reopen-issue
        /// Trigger: issue comment created
        /// Conditions: Issue is closed
        ///             Issue has label "no-recent-activity"
        ///             Issue has label "needs-author-feedback"
        ///             Commenter is the original issue author
        ///             This is not happening because of a "comment with close". This can be checked
        ///             by seeing if the comment's CreatedAt date and issue's ClosedAt date are equal.
        ///             Has this issue been closed for less than 7 days
        /// Resulting Action: 
        ///     Remove "no-recent-activity" label
        ///     Remove "needs-author-feedback" label
        ///     Add "needs-team-attention" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated gitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static void ReopenIssue(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ReopenIssue))
            {
                if (issueCommentPayload.Action == ActionConstants.Created)
                {
                    if (issueCommentPayload.Issue.State == ItemState.Closed &&
                        LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NoRecentActivity) &&
                        LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                        issueCommentPayload.Sender.Login == issueCommentPayload.Issue.User.Login &&
                        issueCommentPayload.Comment.CreatedAt != issueCommentPayload.Issue.ClosedAt.Value &&
                        // Ensure both times are in UTC so timezones don't get tripped up. ClosedAt is nullable
                        // but being that the issue is closed is part of the criteria, this will be set
                        DateTime.UtcNow >= issueCommentPayload.Issue.ClosedAt.Value.UtcDateTime.AddDays(7))
                    {
                        var issueUpdate = gitHubEventClient.GetIssueUpdate(issueCommentPayload.Issue);
                        issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                        issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                        issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                    }
                }
            }
        }

        /// <summary>
        /// Decline to reopen issue https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#decline-to-reopen-issue
        /// Trigger: issue comment created
        /// Conditions: Issue is closed
        ///             Issue has been closed for more than 7 days
        ///             Commenter has collaborator permission of None
        ///             Note: the rule has Commenter is not a contributor, which is incorrect
        ///             Action is not "comment and close"
        /// Resulting Action: 
        ///     Add issue comment
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated gitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static async Task DeclineToReopenIssue(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.DeclineToReopenIssue))
            {
                if (issueCommentPayload.Action == ActionConstants.Created)
                {
                    if (issueCommentPayload.Issue.State == ItemState.Closed &&
                        issueCommentPayload.Comment.CreatedAt != issueCommentPayload.Issue.ClosedAt.Value &&
                        // Ensure both times are in UTC so timezones don't get tripped up. ClosedAt is nullable
                        // but being that the issue is closed is part of the criteria, this will be set.
                        DateTime.UtcNow >= issueCommentPayload.Issue.ClosedAt.Value.UtcDateTime.AddDays(7))
                    {
                        bool hasPermissionOfNone = await gitHubEventClient.DoesUserHavePermission(issueCommentPayload.Repository.Id, issueCommentPayload.Sender.Login, PermissionLevel.None);
                        if (hasPermissionOfNone)
                        {
                            string issueComment = "Thank you for your interest in this issue! Because it has been closed for a period of time, we strongly advise that you open a new issue linking to this to ensure better visibility of your comment.";
                            gitHubEventClient.CreateComment(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Issue Addressed Commands https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#issue-addressed-commands
        /// Trigger: issue comment created
        /// Conditions: Has label "issue-addressed"
        ///             Comment text contains the string "/unresolve"
        /// Resulting Action: 
        ///     This depends on the permissions of the user who made the comment
        ///     If the user is the issue author and has write or admin permissions then
        ///         Reopen the issue
        ///         Remove label "issue-addressed"
        ///         Add label "needs-team-attention"
        ///     else
        ///         Add issue comment: 
        ///         "Hi ${contextualAuthor}, only the original author of the issue can ask that it be unresolved.  
        ///         Please open a new issue with your scenario and details if you would like to discuss this topic with the team."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated gitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static async Task IssueAddressedCommands(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IssueAddressedCommands))
            {
                if (issueCommentPayload.Action == ActionConstants.Created)
                {
                    if (CommentUtils.CommentContainsText(issueCommentPayload.Comment.Body, CommentConstants.Unresolve) &&
                        LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.IssueAddressed))
                    {
                        bool hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(issueCommentPayload.Repository.Id, issueCommentPayload.Sender.Login);

                        // if the user who created the comment is the issue author OR the user has write or admin permission
                        if (issueCommentPayload.Sender.Login == issueCommentPayload.Issue.User.Login ||
                            hasAdminOrWritePermission)
                        {
                            var issueUpdate = gitHubEventClient.GetIssueUpdate(issueCommentPayload.Issue);
                            issueUpdate.State = ItemState.Open;
                            issueUpdate.RemoveLabel(LabelConstants.IssueAddressed);
                            issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                        }
                        // else the user is not the original author AND they don't have admin or write permission
                        else
                        {
                            if (!hasAdminOrWritePermission)
                            {
                                string issueComment = $"Hi ${issueCommentPayload.Sender.Login}, only the original author of the issue can ask that it be unresolved.  Please open a new issue with your scenario and details if you would like to discuss this topic with the team.";
                                gitHubEventClient.CreateComment(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
                            }
                        }
                    }
                }
            }
        }
    }
}
