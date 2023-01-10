using System;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit.Internal;
using Octokit;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    internal class PullRequestCommentProcessing
    {
        internal static async Task ProcessPullRequestCommentEvent(GitHubClient gitHubClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            // The payload for an Issue Comment and PullRequest Comment are both IssueCommentPayloads but
            // in the case of the PullRequest the IssueComment.Issue.PullRequest will be non-null. It's also
            // worth noting that the PullRequest object on the IssueUpdate isn't a full PullRequest object.
            IssueCommentPayload prCommentPayload = serializer.Deserialize<IssueCommentPayload>(rawJson); 
            IssueUpdate issueUpdate = null;

            // If the Issue Comment isn't a PullRequest Comment
            if (prCommentPayload.Issue.PullRequest == null) 
            {
                return;
            }

            issueUpdate = await ResetPullRequestActivity(gitHubClient, prCommentPayload, issueUpdate);
            issueUpdate = await ReopenPullRequest(gitHubClient, prCommentPayload, issueUpdate);

            // If any of the rules have made _issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await EventUtils.UpdateIssueOrPullRequest(gitHubClient, prCommentPayload.Repository.Id, prCommentPayload.Issue.PullRequest.Number, issueUpdate);
            }
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// Normally there's supposed to be a common function but the PullRequest object on the IssueCommentPayload isn't complete.
        /// All of the things like labels and whatnot need to come from the Issue.
        /// Trigger: issue_comment created on a pull request
        /// Conditions for pull request comment
        ///     Pull request has "no-recent-activity" label
        ///     User modifying the pull request is not a bot
        ///     Commenter is not the pull request author
        ///     Comment is not a /check-enforcer or /azp command
        ///     Comment is not from an auto-close event. Note: This could happen if the
        ///     the cron task wasn't executed with secrets.GITHUB_TOKEN (aka someone ran
        ///     the task from their own personal token.)
        ///     Commenter doesn't have Write or Admin Collaborator permissions
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="prCommentPayload">Pull Request Comment event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static async Task<IssueUpdate> ResetPullRequestActivity(GitHubClient gitHubClient,
                                                                         IssueCommentPayload prCommentPayload,
                                                                         IssueUpdate issueUpdate)
        {
            if (prCommentPayload.Sender.Type != AccountType.Bot &&
                LabelUtils.HasLabel(prCommentPayload.Issue.Labels, LabelConstants.NoRecentActivity))
            {
                if (prCommentPayload.Action == ActionConstants.Created &&
                    prCommentPayload.Issue.User.Login != prCommentPayload.Sender.Login &&
                    !EventUtils.CommentContainsText(prCommentPayload.Comment.Body, CommentConstants.CheckEnforcer) &&
                    !EventUtils.CommentContainsText(prCommentPayload.Comment.Body, CommentConstants.Azp) &&
                    !EventUtils.CommentContainsText(prCommentPayload.Comment.Body, CommentConstants.ScheduledCloseFragment))
                {
                    bool hasWriteOrAdminPermissions = await AuthUtils.DoesUserHaveAdminOrWritePermission(gitHubClient, prCommentPayload.Repository.Id, prCommentPayload.Sender.Login);
                    if (!hasWriteOrAdminPermissions)
                    {
                        issueUpdate = EventUtils.GetIssueUpdate(prCommentPayload.Issue, issueUpdate);
                        issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                    }
                }
            }
            return issueUpdate;
        }

        /// <summary>
        /// Reopen Pull Request https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reopen-pull-request
        /// Trigger: pull comment created
        /// Conditions:
        ///     Pull request is closed
        ///     Pull request has "no-recent-activity" label
        ///     Comment text contains the string "/reopen"
        ///     Commenter does not have write permissions AND is NOT the pull request author
        /// Resulting Action:
        ///     Remove "no-recent-activity" label
        ///     Reopen pull request
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="prCommentPayload"></param>
        /// <param name="issueUpdate"></param>
        internal static async Task<IssueUpdate> ReopenPullRequest(GitHubClient gitHubClient, IssueCommentPayload prCommentPayload, IssueUpdate issueUpdate)
        {
            if (prCommentPayload.Action == ActionConstants.Created)
            {
                if (prCommentPayload.Issue.PullRequest.State == ItemState.Closed &&
                    LabelUtils.HasLabel(prCommentPayload.Issue.PullRequest.Labels, LabelConstants.NoRecentActivity) &&
                    EventUtils.CommentContainsText(prCommentPayload.Comment.Body, CommentConstants.Reopen) &&
                    prCommentPayload.Sender.Login != prCommentPayload.Issue.PullRequest.User.Login)
                {
                    bool hasWriteOrAdminPermissions = await AuthUtils.DoesUserHaveAdminOrWritePermission(gitHubClient, prCommentPayload.Repository.Id, prCommentPayload.Sender.Login);
                    if (!hasWriteOrAdminPermissions)
                    {
                        issueUpdate = EventUtils.GetIssueUpdate(prCommentPayload.Issue, issueUpdate);
                        issueUpdate.State = ItemState.Open;
                    }
                }
            }
            return issueUpdate;
        }
    }
}
