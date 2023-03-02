using Octokit;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using System.Text.Json;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class PullRequestProcessing
    {
        /// <summary>
        /// PullRequest rules can be found on the gist, https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#pull-request-rules
        /// Every rule will have it's own function that will be called here, the rule configuration will determine
        /// which rules will execute.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prEventPayload">PullRequestEventGitHubPayload deserialized from the json event payload</param>
        public static async Task ProcessPullRequestEvent(GitHubEventClient gitHubEventClient, PullRequestEventGitHubPayload prEventPayload)
        {
            await PullRequestTriage(gitHubEventClient, prEventPayload);
            ResetPullRequestActivity(gitHubEventClient, prEventPayload);
            await ResetApprovalsForUntrustedChanges(gitHubEventClient, prEventPayload);

            // After all of the rules have been processed, call to process pending updates
            int numUpdates = await gitHubEventClient.ProcessPendingUpdates(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prEventPayload">PullRequestEventGitHubPayload deserialized from the json event payload</param>
        public static async Task PullRequestTriage(GitHubEventClient gitHubEventClient,
                                                   PullRequestEventGitHubPayload prEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.PullRequestTriage))
            {
                if (prEventPayload.Action == ActionConstants.Opened)
                {
                    if (prEventPayload.PullRequest.Labels.Count == 0)
                    {
                        var prFileList = await gitHubEventClient.GetFilesForPullRequest(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
                        var prLabels = CodeOwnerUtils.GetPRAutoLabelsForFilePaths(prEventPayload.PullRequest.Labels, prFileList);
                        if (prLabels.Count > 0)
                        {
                            var issueUpdate = gitHubEventClient.GetIssueUpdate(prEventPayload.PullRequest);
                            foreach (var prLabel in prLabels)
                            {
                                issueUpdate.AddLabel(prLabel);
                            }
                        }

                        // The sender will only have Write or Admin permssion if they are a collaborator
                        bool hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(prEventPayload.Repository.Id, prEventPayload.PullRequest.User.Login);
                        // If the user doesn't have Write or Admin permissions
                        if (!hasAdminOrWritePermission)
                        {
                            var issueUpdate = gitHubEventClient.GetIssueUpdate(prEventPayload.PullRequest);
                            issueUpdate.AddLabel(LabelConstants.CustomerReported);
                            issueUpdate.AddLabel(LabelConstants.CommunityContribution);
                            string prComment = $"Thank you for your contribution @{prEventPayload.PullRequest.User.Login}! We will review the pull request and get back to you soon.";
                            gitHubEventClient.CreateComment(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, prComment);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// See Common_ResetPullRequestActivity function for details
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prEventPayload">PullRequestEventGitHubPayload deserialized from the json event payload</param>
        public static void ResetPullRequestActivity(GitHubEventClient gitHubEventClient,
                                                    PullRequestEventGitHubPayload prEventPayload)
        {
            Common_ResetPullRequestActivity(gitHubEventClient, 
                                            prEventPayload.Action, 
                                            prEventPayload.PullRequest, 
                                            prEventPayload.Repository, 
                                            prEventPayload.Sender);
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// This action has triggers from 3 different events: pull_request and pull_request_review and issue_comment
        /// Note: issue_comment, had to be a different function. While the issue_comment does have a PullRequest on
        /// the issue, it's not a complete PullRequest like what comes in with a pull_request or pull_request_review event.
        /// This function only covers pull_request and pull_request_review
        /// Trigger: 
        ///     pull_request reopened, synchronize (changes pushed), review_requested, merged
        ///     pull_request_review submitted
        /// Conditions for all triggers
        ///     Pull request has "no-recent-activity" label
        ///     User modifying the pull request is not a bot
        /// Conditions for pull request triggers, except for merge
        ///     Pull request is open.
        ///     Action is reopen, synchronize (changed pushed) or review requested
        /// Conditions for pull request merged
        ///     Pull request is closed
        ///     Pull request payload, github.event.pull_request.merged, will be true
        /// Resulting Action: 
        ///     Remove "no-recent-activity" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="action">The action being performed, from the payload object</param>
        /// <param name="pullRequest">Octokit.PullRequest object from the respective payload</param>
        /// <param name="repository">Octokit.Repository object from the respective payload</param>
        /// <param name="sender">Octokit.User from the payload's Sender.</param>
        public static void Common_ResetPullRequestActivity(GitHubEventClient gitHubEventClient,
                                                           string action,
                                                           PullRequest pullRequest,
                                                           Repository repository,
                                                           User sender)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ResetPullRequestActivity))
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
                    // Pull request conditions AND the pull request needs to be in an opened state
                    if ((action == ActionConstants.Reopened || 
                         action == ActionConstants.Synchronize ||
                         action == ActionConstants.ReviewRequested) &&
                         pullRequest.State == ItemState.Open)
                    {
                        removeLabel = true;
                    }
                    // Pull request merged conditions, the merged flag would be true and the PR would be closed
                    else if (action == ActionConstants.Closed &&
                             pullRequest.Merged)
                    {
                        removeLabel = true;
                    }
                    // Pull request reviewed conditions. Submitted is only a pull_request_review event.
                    else if (action == ActionConstants.Submitted)
                    {
                        removeLabel = true;
                    }
                    if (removeLabel)
                    {
                        var issueUpdate = gitHubEventClient.GetIssueUpdate(pullRequest);
                        issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                    }
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prEventPayload">PullRequestEventGitHubPayload deserialized from the json event payload</param>
        public static async Task ResetApprovalsForUntrustedChanges(GitHubEventClient gitHubEventClient,
                                                                   PullRequestEventGitHubPayload prEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ResetApprovalsForUntrustedChanges))
            {
                if (prEventPayload.Action == ActionConstants.Synchronize)
                {
                    if (prEventPayload.PullRequest.State == ItemState.Open &&
                        prEventPayload.AutoMergeEnabled)
                    {
                        bool hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(prEventPayload.Repository.Id, prEventPayload.PullRequest.User.Login);
                        // The sender will only have Write or Admin permssion if they are a collaborator
                        if (!hasAdminOrWritePermission)
                        {
                            // In this case, get all of the reviews 
                            var reviews = await gitHubEventClient.GetReviewsForPullRequest(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
                            foreach (var review in reviews)
                            {
                                // For each review that has approved the pull_request, dismiss it
                                if (review.State == PullRequestReviewState.Approved)
                                {
                                    // Every dismiss needs a dismiss message. Might as well make it personalized.
                                    string dismissalMessage = $"Hi @{review.User.Login}.  We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates.";
                                    gitHubEventClient.DismissReview(prEventPayload.Repository.Id,
                                                                    prEventPayload.PullRequest.Number,
                                                                    review.Id,
                                                                    dismissalMessage);
                                }
                            }
                            string prComment = $"Hi @{prEventPayload.PullRequest.User.Login}. We've noticed that new changes have been pushed to this pull request.  Because it is set to automatically merge, we've reset the approvals to allow the opportunity to review the updates.";
                            gitHubEventClient.CreateComment(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, prComment);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The pull_request, because of the auto_merge processing, requires more than just deserialization of the
        /// the rawJson, it also needs to set whether or not the auto_merge has been enabled. Because this is also
        /// by the static tests it needed to be in a common function.
        /// </summary>
        /// <param name="rawJson">The rawJson to deserialize</param>
        /// <param name="serializer">Octokit.Internal.SimpleJsonSerializer which is serializer used to deserialize the payload into OctoKit classes.</param>
        /// <returns>PullRequestEventGitHubPayload deserialized from the event json</returns>
        public static PullRequestEventGitHubPayload DeserializePullRequest(string rawJson, SimpleJsonSerializer serializer)
        {
            PullRequestEventGitHubPayload prEventPayload = serializer.Deserialize<PullRequestEventGitHubPayload>(rawJson);
            using var doc = JsonDocument.Parse(rawJson);
            // The actions event payload for a pull_request has a class on the pull request that
            // the OctoKit.PullRequest class does not have. This will be null if the user does not
            // have Auto-Merge enabled through the pull request UI and will be non-null otherwise.
            // An AutoMergeEnabled boolean was added to the root of the PullRequestEventGitHubPayload
            // which defaults to false. The actual information in the auto_merge is not necessary
            // for any rules processing other than knowing whether or not it's been set.
            var autoMergeProp = doc.RootElement.GetProperty("pull_request").GetProperty("auto_merge");
            if (JsonValueKind.Object == autoMergeProp.ValueKind)
            {
                prEventPayload.AutoMergeEnabled = true;
            }

            return prEventPayload;
        }
    }
}
