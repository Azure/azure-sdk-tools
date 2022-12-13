using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubAuth;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GithubEventProcessor.Utils;
using Octokit.Internal;
using System.Threading.Tasks;
using Octokit;
using Azure.Sdk.Tools.GithubEventProcessor.Constants;
using System.Security;

namespace Azure.Sdk.Tools.GithubEventProcessor.EventProcessing
{
    // Issue Comment Processing also includes PR comments. The guidance for pull_request_comment
    // say to use the issue comment event
    // https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#pull_request_comment-use-issue_comment
    //
    // If there is a rule that requires processing on issue/issue_comment, then the logic should be in a separate
    // function inside of IssueProcessing that both will call. 
    // It's also worth noting that the Octokit's IssueCommentPayload has parity to the GitHub Payload
    // which means it can be used as-is for deserialization.
    internal class IssueCommentProcessing
    {
        internal static async Task ProcessIssueCommentEvent(GitHubClient gitHubClient, string rawJson)
        {

            var serializer = new SimpleJsonSerializer();
            IssueCommentPayload issueCommentPayload = serializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueUpdate issueUpdate = null;

            issueUpdate = await AuthorFeedback(gitHubClient, issueCommentPayload, issueUpdate);
            ResetIssueActivity(gitHubClient, issueCommentPayload, ref issueUpdate);
            ReopenIssue(gitHubClient, issueCommentPayload, ref issueUpdate);
            // DeclineToReopenIssue creates a comment and does not use issueUpdate
            await DeclineToReopenIssue(gitHubClient, issueCommentPayload);

            // If any of the rules have made issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await gitHubClient.Issue.Update(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueUpdate);
            }
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
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static async Task<IssueUpdate> AuthorFeedback(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload, IssueUpdate issueUpdate)
        {
            if (String.Equals(issueCommentPayload.Action, ActionConstants.Created))
            {
                if (issueCommentPayload.Issue.State == ItemState.Open &&
                    LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                    issueCommentPayload.Comment.User.Login == issueCommentPayload.Issue.User.Login)
                {
                    issueUpdate = IssueUtils.GetIssueUpdate(issueCommentPayload.Issue, issueUpdate); issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                    string issueComment =
                        @$"Hi @{issueCommentPayload.Issue.User.Login} . Thank you for opening this issue and giving us the opportunity 
to assist. To help our team better understand your issue and the details of your scenario please provide a 
response to the question asked above or the information requested above. This will help us more accurately 
address your issue.";
                    await IssueUtils.CreateComment(gitHubClient, issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
                }
            }
            return issueUpdate;
        }
        /// <summary>
        /// Reset Issue Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-issue-activity
        /// See Common_ResetIssueActivity comments
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void ResetIssueActivity(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload, ref IssueUpdate issueUpdate)
        {
            IssueProcessing.Common_ResetIssueActivity(gitHubClient, issueCommentPayload.Action, issueCommentPayload.Issue, issueCommentPayload.Comment.User, ref issueUpdate);
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
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void ReopenIssue(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload, ref IssueUpdate issueUpdate)
        {
            if (issueCommentPayload.Action == ActionConstants.Created)
            {
                if (issueCommentPayload.Issue.State == ItemState.Closed &&
                    LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NoRecentActivity) &&
                    LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                    issueCommentPayload.Comment.User.Login == issueCommentPayload.Issue.User.Login &&
                    issueCommentPayload.Comment.CreatedAt == issueCommentPayload.Issue.ClosedAt.Value &&
                    // Ensure both times are in UTC so timezones don't get tripped up. ClosedAt is nullable
                    // but being that the issue is closed is part of the criteria, this will be set
                    issueCommentPayload.Issue.ClosedAt.Value.UtcDateTime.AddDays(7) >= DateTime.UtcNow)
                {
                    issueUpdate = IssueUtils.GetIssueUpdate(issueCommentPayload.Issue, issueUpdate); 
                    issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                    issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                    issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
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
        ///     Add "needs-team-attention" label
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        internal static async Task DeclineToReopenIssue(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload)
        {
            if (issueCommentPayload.Action == ActionConstants.Created)
            {
                if (issueCommentPayload.Issue.State == ItemState.Closed &&
                    // Ensure both times are in UTC so timezones don't get tripped up. ClosedAt is nullable
                    // but being that the issue is closed is part of the criteria, this will be set.
                    issueCommentPayload.Issue.ClosedAt.Value.UtcDateTime.AddDays(7) < DateTime.UtcNow &&
                    issueCommentPayload.Comment.CreatedAt == issueCommentPayload.Issue.ClosedAt.Value)
                {
                    bool hasPermissionOfNone = await AuthUtils.DoesUserHavePermission(gitHubClient, issueCommentPayload.Repository.Id, issueCommentPayload.Comment.User.Login, PermissionLevel.None);
                    if (hasPermissionOfNone)
                    {
                        string issueComment = "Thank you for your interest in this issue! Because it has been closed for a period of time, we strongly advise that you open a new issue linking to this to ensure better visibility of your comment.";
                        await IssueUtils.CreateComment(gitHubClient, issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
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
        ///         Add issue comment
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueCommentPayload">issue_comment event payload</param>
        internal static async Task<IssueUpdate> IssueAddressedCommands(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload, IssueUpdate issueUpdate)
        {
            if (issueCommentPayload.Action == ActionConstants.Created)
            {
                if (issueCommentPayload.Comment.Body.Contains(CommentConstants.Unresolve) &&
                    LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.IssueAddressed))
                {
                    List<PermissionLevel> permissionList = new List<PermissionLevel>
                    {
                        PermissionLevel.Admin,
                        PermissionLevel.Write
                    };
                    bool hasAdminOrWritePermission = await AuthUtils.DoesUserHavePermissions(gitHubClient, issueCommentPayload.Repository.Id, issueCommentPayload.Comment.User.Login, permissionList);

                    // if the user who created the comment is the issue author OR the user has write or admin permission
                    if (issueCommentPayload.Comment.User.Login == issueCommentPayload.Issue.User.Login ||
                        hasAdminOrWritePermission)
                    {
                        issueUpdate = IssueUtils.GetIssueUpdate(issueCommentPayload.Issue, issueUpdate);
                        issueUpdate.State = ItemState.Open;
                        issueUpdate.RemoveLabel(LabelConstants.IssueAddressed);
                        issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                    }
                    // else the user is not the original author AND they don't have admin or write permission
                    else
                    {
                        if (!hasAdminOrWritePermission)
                        {
                            string issueComment = $"Hi ${issueCommentPayload.Comment.User.Login} , only the original author of the issue can ask that it be unresolved.  Please open a new issue with your scenario and details if you would like to discuss this topic with the team.";
                            await IssueUtils.CreateComment(gitHubClient, issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);
                        }
                    }
                }
            }
            return issueUpdate;
        }
    }
}
