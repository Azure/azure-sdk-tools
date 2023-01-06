using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubAuth;
using Octokit.Internal;
using Octokit;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GithubEventProcessor.Utils;
using System.Reflection.Emit;
using System.Linq;
using Azure.Sdk.Tools.GithubEventProcessor.Constants;

namespace Azure.Sdk.Tools.GithubEventProcessor.EventProcessing
{
    internal class PullRequestProcessing
    {
        internal static async Task ProcessPullRequestEvent(GitHubClient gitHubClient, PullRequestEventGitHubPayload prEventPayload)
        {
            IssueUpdate issueUpdate = null;

            issueUpdate = await PullRequestTriage(gitHubClient, prEventPayload, issueUpdate);
            ResetPullRequestActivity(gitHubClient, prEventPayload, ref issueUpdate);

            // If any of the rules have made issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await EventUtils.UpdateIssueOrPullRequest(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, issueUpdate);
            }
        }


        /// <summary>
        /// Pull Request Triage https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#pull-request-triage
        /// Trigger: pull request opened
        /// Conditions: Pull request has no labels
        /// Resulting Action: 
        ///     Evaluate the path for each file in the PR, if the path has a label, add the label to the issue
        ///     If the sender is not a Collaborator OR, if they are a collaborator without Write/Admin permissions
        ///         Add "customer-reported" label
        ///         Add "Community Contribution" label
        ///         Create issue comment: "Thank you for your contribution @{issueAuthor} ! We will review the pull request and get back to you soon."
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="prEventPayload">Pull Request event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static async Task<IssueUpdate> PullRequestTriage(GitHubClient gitHubClient,
                                                                  PullRequestEventGitHubPayload prEventPayload,
                                                                  IssueUpdate issueUpdate)
        {
            if (prEventPayload.Action == ActionConstants.Opened)
            {
                if (prEventPayload.PullRequest.Labels.Count == 0)
                {
                    var prFileList = await EventUtils.GetFilesForPullRequest(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
                    var prLabels = CodeOwnerUtils.getPRAutoLabelsForFilePaths(prEventPayload.PullRequest.Labels, prFileList);
                    if (prLabels.Count > 0)
                    {
                        issueUpdate = EventUtils.GetIssueUpdate(prEventPayload.PullRequest, issueUpdate);
                        foreach (var prLabel in prLabels)
                        {
                            issueUpdate.AddLabel(prLabel);
                        }
                    }

                    bool hasAdminOrWritePermission = await AuthUtils.DoesUserHaveAdminOrWritePermission(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.User.Login);
                    // The sender will only have Write or Admin permssion if they are a collaborator
                    if (hasAdminOrWritePermission)
                    {
                        issueUpdate = EventUtils.GetIssueUpdate(prEventPayload.PullRequest, issueUpdate);
                        issueUpdate.AddLabel(LabelConstants.CustomerReported);
                        issueUpdate.AddLabel(LabelConstants.CommunityContribution);
                        string prComment = $"Thank you for your contribution @{prEventPayload.PullRequest.User.Login}! We will review the pull request and get back to you soon.";
                        await EventUtils.CreateComment(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, prComment);
                    }
                }
            }
            return issueUpdate;
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// See Common_ResetPullRequestActivity function for details
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="prEventPayload">Pull Request event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static void ResetPullRequestActivity(GitHubClient gitHubClient,
                                                      PullRequestEventGitHubPayload prEventPayload,
                                                      ref IssueUpdate issueUpdate)
        {
            Common_ResetPullRequestActivity(gitHubClient, 
                                            prEventPayload.Action, 
                                            prEventPayload.PullRequest, 
                                            prEventPayload.Repository, 
                                            prEventPayload.Sender, 
                                            ref issueUpdate);
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// This action has triggers from 3 different events: pull_request and pull_request_review and issue_comment
        /// Note: issue_comment, for a pull request, will have a non-null github.event.issue.pull_request in the payload
        /// Trigger: 
        ///     pull_request reopened, synchronize (changes pushed), review_requested, merged
        ///     pull_request_review submitted
        ///     issue_comment created
        /// Conditions for all triggers
        ///     Pull request has "no-recent-activity" label
        ///     User modifying the pull request is not a bot
        /// Conditions for pull request triggers, except for merge
        ///     Pull request is open.
        ///     Action is reopen, synchronize or review requested
        /// Conditions for pull request merged
        ///     Pull request is closed
        ///     Pull request payload, github.event.pull_request.merged, will be true
        /// Resulting Action: 
        ///     Remove "no-recent-activity" label
        ///     Reopen pull request
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="action">The action being performed, from the payload object</param>
        /// <param name="pullRequest">Octokit.PullRequest object from the respective payload</param>
        /// <param name="sender">Octokit.User object from the respective payload. This will be the Sender that initiated the event.</param>
        /// <param name="comment">The comment, if triggered by comment, null otherwise</param>
        /// <param name="issueUpdate">The issue update object</param>
        public static void Common_ResetPullRequestActivity(GitHubClient gitHubClient,
                                                           string action,
                                                           PullRequest pullRequest,
                                                           Repository repository,
                                                           User sender,
                                                           ref IssueUpdate issueUpdate)
        {
            // Normally the action would be checked first but the various events and their conditions
            // all have two checks in common which are quick and would alleviate the need to check anything
            // else.
            // 1. The sender is not a bot.
            // 2. The Pull request has "no-recent-activity" label
            if (sender.Type != AccountType.Bot && 
                LabelUtils.HasLabel(pullRequest.Labels, LabelConstants.NoRecentActivity))
            {
                bool removeLabel = false;
                // Conditions if the event is a pull request event
                if ((action == ActionConstants.Reopened ||
                     action == ActionConstants.Synchronize ||
                     action == ActionConstants.ReviewRequested) &&
                     pullRequest.State == ItemState.Open)
                {
                    removeLabel = true;
                }
                // Conditions for pull request merged
                else if (action == ActionConstants.Closed &&
                         pullRequest.Merged)
                {
                    removeLabel = true;
                }
                if (removeLabel)
                {
                    issueUpdate = EventUtils.GetIssueUpdate(pullRequest, issueUpdate);
                    issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                    issueUpdate.State = ItemState.Open;
                }
            }
        }

        /// <summary>
        /// Reset auto-merge approvals on untrusted changes https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-auto-merge-approvals-on-untrusted-changes
        /// Trigger: pull request synchronized
        /// Conditions:
        ///     Pull request is open
        ///     Pull request has auto-merge enabled
        ///     User who pushed the changes does NOT have a collaborator association
        ///     User who pushed changes does NOT have write permission
        ///     User who pushed changes does NOT have admin permission
        /// Resulting Action: 
        ///     Reset all approvals
        ///     Create issue comment: "Hi @{issueAuthor}.  We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates."
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="prEventPayload"></param>
        /// <returns></returns>
        internal static async Task ResetApprovalsForUntrustedChanges(GitHubClient gitHubClient,
                                                                     PullRequestEventGitHubPayload prEventPayload)
        {
            if (prEventPayload.Action == ActionConstants.Synchronize)
            {
                if (prEventPayload.PullRequest.State== ItemState.Open &&
                    prEventPayload.AutoMergeEnabled)
                {
                    bool hasAdminOrWritePermission = await AuthUtils.DoesUserHaveAdminOrWritePermission(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.User.Login);
                    // The sender will only have Write or Admin permssion if they are a collaborator
                    if (!hasAdminOrWritePermission)
                    {
                        // In this case, get all of the reviews 
                        var reviews = await gitHubClient.PullRequest.Review.GetAll(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
                        foreach (var review in reviews)
                        {
                            // For each review that has approved the pull_request, dismiss it
                            if (review.State == PullRequestReviewState.Approved)
                            {
                                // Every dismiss needs a dismiss message. Might as well make it personalized.
                                var prReview = new PullRequestReviewDismiss();
                                prReview.Message = $"Hi @{review.User.Login}.  We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates.";
                                await gitHubClient.PullRequest.Review.Dismiss(prEventPayload.Repository.Id, 
                                                                              prEventPayload.PullRequest.Number,
                                                                              review.Id, 
                                                                              prReview);
                            }
                        }

                        string prComment = $"Hi @{prEventPayload.PullRequest.User.Login}. We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates.";
                        await EventUtils.CreateComment(gitHubClient, prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, prComment);
                    }
                }
            }
            return;
        }

        // JRS - everything below here was here for experimental purposes and will be removed

        internal static async Task TestFunctionToGetLimitCountsForPRFileFetching(GitHubClient gitHubClient)
        {
            // Azure/azure-sdk-for-net repo Id = 2928944
            // https://github.com/Azure/azure-sdk-for-net/pull/32301 - PR has 30 files
            // Azure/azure-sdk-for-java repo Id = 2928948
            // https://github.com/Azure/azure-sdk-for-java/pull/31960 has 318 files
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit before fetching pull request:");
            // https://github.com/Azure/azure-sdk-for-java/pull/31960 has 318 files
            var pullRequest = await gitHubClient.PullRequest.Get(2928948, 31960);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit after fetching PR, before fetching PR files:");
            var prFileList1 = await gitHubClient.PullRequest.Files(2928948, pullRequest.Number);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit after fetching PR files with PageSize 30 (default):");

            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            var prFileList = await gitHubClient.PullRequest.Files(2928948, pullRequest.Number, apiOptions);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit after fetching PR files with PageSize 100:");
        }

        internal static async Task DismissPullRequestApprovals(GitHubClient gitHubClient, string rawJson)
        {
            // Get all of the reviews for a given PR. In this case 507980610 is the JimSuplizio/azure-sdk-tools
            // and 8 is the Issue/PR number
            // Java repo ID=2928948
            // Java repo test PR=https://github.com/Azure/azure-sdk-for-java/pull/32108
            var reviews = await gitHubClient.PullRequest.Review.GetAll(2928948, 32108);
            foreach(var review in reviews)
            {
                if (review.State == PullRequestReviewState.Approved)
                {
                    // JRS - *sigh* every dismiss needs a dismiss message
                    var prReview = new PullRequestReviewDismiss();
                    prReview.Message = $"Hi @alzimmermsft.  We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates.";
                    await gitHubClient.PullRequest.Review.Dismiss(2928948, 32108, review.Id, prReview);
                }
            }
            return;
        }
    }
}
