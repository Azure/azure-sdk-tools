using System;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using Octokit;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    public class EventUtils
    {
        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate.
        /// </summary>
        /// <param name="issue">Octokit.Issue from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public static IssueUpdate GetIssueUpdate(Issue issue, IssueUpdate issueUpdate)
        {
            if (null == issueUpdate)
            {
                return issue.ToUpdate();
            }
            return issueUpdate;
        }

        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate. 
        /// </summary>
        /// <param name="pullRequest">Octokit.PullRequest from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public static IssueUpdate GetIssueUpdate(PullRequest pullRequest, IssueUpdate issueUpdate)
        {
            if (null == issueUpdate)
            {
                return CreateIssueUpdateForPR(pullRequest);
            }
            return issueUpdate;
        }

        /// <summary>
        /// Create an IssueUpdate for a PR. For Issues, creating an IssueUpdate is done calling
        /// Issue.ToUpdate() on the Issue contained within the IssueEventGitHubPayload which
        /// create an IssueUpdate prefilled with information from the issue. For PullRequests,
        /// there is no such call to create an IssueUpdate. The IssueUpdate needs this prefilled
        /// information otherwise, it'll end clearing/resetting things. This code is, quite 
        /// literally, taken directly from Issue's ToUpdate call and modified to get the
        /// information from the input PullRequest.
        /// I filed an issue about this with Octokit.Net https://github.com/octokit/octokit.net/discussions/2629
        /// </summary>
        /// <param name="pullRequest">Octokit.PullRequest object from event payload</param>
        /// <returns>OctoKit.IssueUpdate</returns>
        internal static IssueUpdate CreateIssueUpdateForPR(PullRequest pullRequest)
        {
            var milestoneId = pullRequest.Milestone == null
                ? new int?()
                : pullRequest.Milestone.Number;

            var assignees = pullRequest.Assignees == null
                ? null
                : pullRequest.Assignees.Select(x => x.Login);

            var labels = pullRequest.Labels == null
            ? null
                : pullRequest.Labels.Select(x => x.Name);

            ItemState state;
            var issueUpdate = new IssueUpdate
            {
                Body = pullRequest.Body,
                Milestone = milestoneId,
                State = (pullRequest.State.TryParse(out state) ? (ItemState?)state : null),
                Title = pullRequest.Title
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="textToLookFor"></param>
        /// <returns></returns>
        internal static bool CommentContainsText(string comment, string textToLookFor)
        {
            // Why is this using IndexOf instead of string.Contains?
            // This is the reason https://learn.microsoft.com/en-us/dotnet/api/system.string.contains?view=netframework-4.8
            // The overload for contains with a StringComparison only exists in .NET Core, not the full Framework.
            // Doing it this way makes it resilient regardless of how things get compiled.
            // Also, the strings being looked for will always be in English, matching what's in the
            // CommentConstants class which is why OrdinalIgnoreCase instead of the cultural string
            // comparisons.
            if (comment.IndexOf(textToLookFor, StringComparison.OrdinalIgnoreCase) > 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Common function to add a comment to an Issue or PullRequest. Behind the scenes
        /// Issues and PullRequests are the same when it comes to creating comments directly on
        /// them.
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="repositoryId">The Id of the repository, pulled from the payload</param>
        /// <param name="issueOrPullRequestNumber">The Issue or PullRequest number, pulled from the payload</param>
        /// <param name="comment">The comment being created.</param>
        /// <returns></returns>
        internal static async Task CreateComment(GitHubClient gitHubClient,
                                                 long repositoryId,
                                                 int issueOrPullRequestNumber,
                                                 string comment)
        {
            await gitHubClient.Issue.Comment.Create(repositoryId, issueOrPullRequestNumber, comment);
        }


        /// <summary>
        /// Common function to update an Issue or Pull Request from an IssueUpdate instance.
        /// Behind the scenes, Issues and PullRequests are the both updated using an IssueUpdate.
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="repositoryId">The Id of the repository, pulled from the payload</param>
        /// <param name="issueOrPullRequestNumber">The Issue or PullRequest number, pulled from the payload</param>
        /// <param name="issueUpdate">The IssueUpdate instance that contains the updated information</param>
        /// <returns></returns>
        internal static async Task UpdateIssueOrPullRequest(GitHubClient gitHubClient, 
                                                            long repositoryId, 
                                                            int issueOrPullRequestNumber, 
                                                            IssueUpdate issueUpdate)
        {
            try
            {
                await gitHubClient.Issue.Update(repositoryId, issueOrPullRequestNumber, issueUpdate);
            }
            catch (Exception ex)
            {
                // JRS - what to do if this throws?
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Common function to get files for a pull request. The default page size for the API is 30
        /// and needs to be set to 100 to minimize calls, do that here.
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="repositoryId"></param>
        /// <param name="pullRequestNumber"></param>
        /// <returns></returns>
        internal static async Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(GitHubClient gitHubClient,
                                                                                          long repositoryId,
                                                                                          int pullRequestNumber)
        {
            // For whatever reason the default page size
            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            return await gitHubClient.PullRequest.Files(repositoryId, pullRequestNumber, apiOptions);
        }
    }
}
