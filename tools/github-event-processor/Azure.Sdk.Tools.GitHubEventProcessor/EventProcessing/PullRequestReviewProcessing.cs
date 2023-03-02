using System;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit.Internal;
using Octokit;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class PullRequestReviewProcessing
    {
        /// <summary>
        /// PullRequest rules can be found on the gist, https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#pull-request-rules
        /// Every rule will have it's own function that will be called here, the rule configuration will determine
        /// which rules will execute.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prReviewEventPayload">PullRequestReviewEventPayload deserialized from the json event payload</param>
        public static async Task ProcessPullRequestReviewEvent(GitHubEventClient gitHubEventClient, PullRequestReviewEventPayload prReviewEventPayload)
        {
            ResetPullRequestActivity(gitHubEventClient, prReviewEventPayload);

            // After all of the rules have been processed, call to process pending updates
            await gitHubEventClient.ProcessPendingUpdates(prReviewEventPayload.Repository.Id, prReviewEventPayload.PullRequest.Number);
        }

        /// <summary>
        /// Reset Pull Request Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-pull-request-activity
        /// See Common_ResetPullRequestActivity function for details
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="prReviewEventPayload">PullRequestReviewEventPayload deserialized from the json event payload</param>
        /// <returns></returns>
        public static void ResetPullRequestActivity(GitHubEventClient gitHubEventClient,
                                                    PullRequestReviewEventPayload prReviewEventPayload)
        {
            PullRequestProcessing.Common_ResetPullRequestActivity(gitHubEventClient, 
                                                                  prReviewEventPayload.Action, 
                                                                  prReviewEventPayload.PullRequest, 
                                                                  prReviewEventPayload.Repository, 
                                                                  prReviewEventPayload.Sender);
        }
    }
}
