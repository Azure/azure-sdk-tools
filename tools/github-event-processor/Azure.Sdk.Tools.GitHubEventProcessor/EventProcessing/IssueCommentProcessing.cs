using System;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using System.Threading.Tasks;
using Octokit;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class IssueCommentProcessing
    {

        /// <summary>
        /// Every rule will have it's own function that will be called here, the rule configuration will determine
        /// which rules will execute.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueCommentPayload">IssueCommentPayload deserialized from the json event payload</param>
        public static async Task ProcessIssueCommentEvent(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            AuthorFeedback(gitHubEventClient, issueCommentPayload);
            ResetIssueActivity(gitHubEventClient, issueCommentPayload);
            ReopenIssue(gitHubEventClient, issueCommentPayload);
            await DeclineToReopenIssue(gitHubEventClient, issueCommentPayload);
            await IssueAddressedCommands(gitHubEventClient, issueCommentPayload);

            // After all of the rules have been processed, call to process pending updates
            await gitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
        }

        /// <summary>
        /// Author Feedback
        /// Trigger: issue comment created
        /// Conditions: Issue is open
        ///             Issue has "needs-author-feedback" label
        ///             Commenter is the original issue author
        /// Resulting Action: 
        ///     Remove "needs-author-feedback" label
        ///     Add "needs-team-attention" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
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
                        gitHubEventClient.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                        gitHubEventClient.AddLabel(LabelConstants.NeedsTeamAttention);
                    }
                }
            }
        }
        /// <summary>
        /// Reset Issue Activity
        /// For issue_comments, the trigger Action is created 
        /// See Common_ResetIssueActivity comments
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        public static void ResetIssueActivity(GitHubEventClient gitHubEventClient, IssueCommentPayload issueCommentPayload)
        {
            if (issueCommentPayload.Action == ActionConstants.Created)
            {
                IssueProcessing.Common_ResetIssueActivity(gitHubEventClient, issueCommentPayload.Action, issueCommentPayload.Issue, issueCommentPayload.Sender);
            }
        }

        /// <summary>
        /// Reopen Issue
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
        ///     Reopen the issue
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
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
                        DateTime.UtcNow <= issueCommentPayload.Issue.ClosedAt.Value.UtcDateTime.AddDays(7))
                    {
                        gitHubEventClient.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                        gitHubEventClient.RemoveLabel(LabelConstants.NoRecentActivity);
                        gitHubEventClient.AddLabel(LabelConstants.NeedsTeamAttention);
                        gitHubEventClient.SetIssueState(issueCommentPayload.Issue, ItemState.Open);
                    }
                }
            }
        }

        /// <summary>
        /// Decline To Reopen Issue
        /// Trigger: issue comment created
        /// Conditions: Issue is closed
        ///             Issue has been closed for more than 7 days
        ///             Commenter has collaborator permission of None
        ///             Note: the rule has Commenter is not a contributor, which is incorrect
        ///             Action is not "comment and close"
        /// Resulting Action: 
        ///     Add issue comment
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
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
        /// Issue Addressed Commands
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
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
                            gitHubEventClient.SetIssueState(issueCommentPayload.Issue, ItemState.Open);
                            gitHubEventClient.RemoveLabel(LabelConstants.IssueAddressed);
                            gitHubEventClient.AddLabel(LabelConstants.NeedsTeamAttention);
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
