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

namespace Azure.Sdk.Tools.GithubEventProcessor.EventProcessing
{
    internal class PullRequestProcessing
    {
        internal static async Task ProcessPullRequestEvent(GitHubClient gitHubClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            PullRequestEventGitHubPayload prEventPayload = serializer.Deserialize<PullRequestEventGitHubPayload>(rawJson);

            Console.WriteLine(prEventPayload.PullRequest.Title);

            var prFileList = await gitHubClient.PullRequest.Files(prEventPayload.Repository.Id, prEventPayload.Number);
        }

        internal static async Task TestFunctionToGetPRLablesForFiles(GitHubClient gitHubClient)
        {
            // Azure/azure-sdk-for-net repo Id = 2928944
            // https://github.com/Azure/azure-sdk-for-net/pull/32301 - PR has 30 files
            // string codeOwnersPath = @"C:\src\azure-sdk-for-net\.github\CODEOWNERS";
            // Azure/azure-sdk-for-java repo Id = 2928948
            // https://github.com/Azure/azure-sdk-for-java/pull/31960 has 318 files
            CodeOwnerUtils.codeOwnersFilePathOverride = @"C:\src\azure-sdk-for-java\.github\CODEOWNERS";
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit before fetching pull request:");
            var pullRequest = await gitHubClient.PullRequest.Get(2928948, 31960);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit after fetching PR, before fetching PR files:");

            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            var prFileList = await gitHubClient.PullRequest.Files(2928948, pullRequest.Number, apiOptions);
            await RateLimitUtil.writeRateLimits(gitHubClient, "RateLimit after fetching PR files with PageSize 100:");
            var prLabels = CodeOwnerUtils.getPRAutoLabelsForFilePaths(pullRequest.Labels, prFileList);
            foreach (var prLabel in prLabels)
            {
                Console.WriteLine(prLabel);
            }
        }

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

        internal static async Task UpdatePRLabel(GitHubClient gitHubClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            PullRequestEventGitHubPayload prEventPayload = serializer.Deserialize<PullRequestEventGitHubPayload>(rawJson);

            var issueUpdate = CreateIssueUpdateForPR(prEventPayload);
            issueUpdate.AddLabel("enhancement");
            //await gitHubClient.Issue.Update(507980610, 8, issueUpdate);
            await gitHubClient.Issue.Update(prEventPayload.Repository.Id, prEventPayload.Number, issueUpdate);
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

        /// <summary>
        /// Create an IssueUpdate for a PR. For Issues, creating an IssueUpdate is done calling
        /// Issue.ToUpdate() on the Issue contained within the IssueEventGitHubPayload which
        /// create an IssueUpdate prefilled with information from the issue. For PRs, there is no
        /// such call to create an IssueUpdate. The IssueUpdate needs this prefilled information
        /// otherwise, 
        /// </summary>
        /// <param name="prEventPayload"></param>
        /// <returns></returns>
        public static IssueUpdate CreateIssueUpdateForPR(PullRequestEventGitHubPayload prEventPayload)
        {
            var milestoneId = prEventPayload.PullRequest.Milestone == null
                ? new int?()
                : prEventPayload.PullRequest.Milestone.Number;

            var assignees = prEventPayload.PullRequest.Assignees == null
                ? null
                : prEventPayload.PullRequest.Assignees.Select(x => x.Login);

            var labels = prEventPayload.PullRequest.Labels == null
            ? null
                : prEventPayload.PullRequest.Labels.Select(x => x.Name);

            ItemState state;
            var issueUpdate = new IssueUpdate
            {
                Body = prEventPayload.PullRequest.Body,
                Milestone = milestoneId,
                State = (prEventPayload.PullRequest.State.TryParse(out state) ? (ItemState?)state : null),
                Title = prEventPayload.PullRequest.Title
            };

            if (assignees != null)
            {
                foreach (var assignee in assignees)
                {
                    issueUpdate.AddAssignee(assignee);
                }
            }

            if (labels != null)
            {
                foreach (var label in labels)
                {
                    issueUpdate.AddLabel(label);
                }
            }
            return issueUpdate;
        }
    }
}
