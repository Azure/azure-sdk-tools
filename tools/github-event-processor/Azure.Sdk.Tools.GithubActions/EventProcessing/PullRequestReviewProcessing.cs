using System;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.GithubEventProcessor.Utils;
using Octokit.Internal;
using Octokit;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GithubEventProcessor.EventProcessing
{
    internal class PullRequestReviewProcessing
    {
        internal static async Task ProcessPullRequestReviewEvent(GitHubClient gitHubClient, PullRequestReviewEventPayload prReviewEventPayload)
        {
            IssueUpdate issueUpdate = null;

            ResetPullRequestActivity(gitHubClient, prReviewEventPayload, ref issueUpdate);
            // If any of the rules have made issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await EventUtils.UpdateIssueOrPullRequest(gitHubClient, prReviewEventPayload.Repository.Id, prReviewEventPayload.PullRequest.Number, issueUpdate);
            }
        }
        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// See Common_ResetPullRequestActivity function for details
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="prReviewEventPayload">Pull Request Review event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static void ResetPullRequestActivity(GitHubClient gitHubClient,
                                                      PullRequestReviewEventPayload prReviewEventPayload,
                                                      ref IssueUpdate issueUpdate)
        {
            PullRequestProcessing.Common_ResetPullRequestActivity(gitHubClient, 
                                                                  prReviewEventPayload.Action, 
                                                                  prReviewEventPayload.PullRequest, 
                                                                  prReviewEventPayload.Repository, 
                                                                  prReviewEventPayload.Sender, 
                                                                  ref issueUpdate);
        }
    }
}
