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
            await gitHubEventClient.ProcessPendingUpdates(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
        }


        /// <summary>
        /// Pull Request Triage
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
                            foreach (var prLabel in prLabels)
                            {
                                gitHubEventClient.AddLabel(prLabel);
                            }
                        }

                        // If the user is not a member of the Azure Org AND the user does not have write or admin collaborator permission
                        bool isMemberOfOrg = await gitHubEventClient.IsUserMemberOfOrg(OrgConstants.Azure, prEventPayload.PullRequest.User.Login);
                        if (!isMemberOfOrg)
                        {
                            bool hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(prEventPayload.Repository.Id, prEventPayload.PullRequest.User.Login);
                            if (!hasAdminOrWritePermission)
                            {
                                gitHubEventClient.AddLabel(LabelConstants.CustomerReported);
                                gitHubEventClient.AddLabel(LabelConstants.CommunityContribution);
                                string prComment = $"Thank you for your contribution @{prEventPayload.PullRequest.User.Login}! We will review the pull request and get back to you soon.";
                                gitHubEventClient.CreateComment(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number, prComment);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reset Pull Request Activity
        /// This action has triggers from 2 different events: pull_request and issue_comment
        /// Note: issue_comment, had to be a different function. While the issue_comment does have a PullRequest on
        /// the issue, it's not a complete PullRequest like what comes in with a pull_request event.
        /// This function only covers pull_request
        /// Trigger: 
        ///     pull_request reopened, synchronize (changes pushed), review_requested, merged
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
        /// <param name="prEventPayload">PullRequestEventGitHubPayload deserialized from the json event payload</param>
        public static void ResetPullRequestActivity(GitHubEventClient gitHubEventClient,
                                                    PullRequestEventGitHubPayload prEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ResetPullRequestActivity))
            {
                // Normally the action would be checked first but the various events and their conditions
                // all have two checks in common which are quick and would alleviate the need to check anything
                // else.
                // 1. The sender is not a bot.
                // 2. The Pull request has "no-recent-activity" label
                if (prEventPayload.Sender.Type != AccountType.Bot &&
                    LabelUtils.HasLabel(prEventPayload.PullRequest.Labels, LabelConstants.NoRecentActivity))
                {
                    bool removeLabel = false;
                    // Pull request conditions AND the pull request needs to be in an opened state
                    if ((prEventPayload.Action == ActionConstants.Reopened ||
                         prEventPayload.Action == ActionConstants.Synchronize ||
                         prEventPayload.Action == ActionConstants.ReviewRequested) &&
                         prEventPayload.PullRequest.State == ItemState.Open)
                    {
                        removeLabel = true;
                    }
                    // Pull request merged conditions, the merged flag would be true and the PR would be closed
                    else if (prEventPayload.Action == ActionConstants.Closed &&
                             prEventPayload.PullRequest.Merged)
                    {
                        removeLabel = true;
                    }
                    if (removeLabel)
                    {
                        gitHubEventClient.RemoveLabel(LabelConstants.NoRecentActivity);
                    }
                }
            }
        }

        /// <summary>
        /// Reset approvals on untrusted changes if auto-merge is enabled
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
