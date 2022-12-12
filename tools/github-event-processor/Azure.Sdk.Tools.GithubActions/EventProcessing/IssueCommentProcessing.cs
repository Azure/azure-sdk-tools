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
                    if (null == issueUpdate)
                    {
                        issueUpdate = issueCommentPayload.Issue.ToUpdate();
                    }
                    issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                    string issueComment = 
                        @$"Hi @{issueCommentPayload.Issue.User.Login} . Thank you for opening this issue and giving us the opportunity 
to assist. To help our team better understand your issue and the details of your scenario please provide a 
response to the question asked above or the information requested above. This will help us more accurately 
address your issue.";
                    await gitHubClient.Issue.Comment.Create(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number, issueComment);

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
        /// Resulting Action: 
        ///     Remove "no-recent-activity" label
        ///     Remove "needs-author-feedback" label
        ///     Add "needs-team-attention" label
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="issueCommentPayload"></param>
        /// <param name="issueUpdate"></param>
        internal static void ReopenIssue(GitHubClient gitHubClient, IssueCommentPayload issueCommentPayload, ref IssueUpdate issueUpdate)
        {
            if (issueCommentPayload.Issue.State == ItemState.Closed &&
                LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NoRecentActivity) &&
                LabelUtils.HasLabel(issueCommentPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                issueCommentPayload.Comment.User.Login == issueCommentPayload.Issue.User.Login &&
                issueCommentPayload.Comment.CreatedAt == issueCommentPayload.Issue.ClosedAt)
            {
                if (null == issueUpdate)
                {
                    issueUpdate = issueCommentPayload.Issue.ToUpdate();
                }
                issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
            }
        }
    }
}
