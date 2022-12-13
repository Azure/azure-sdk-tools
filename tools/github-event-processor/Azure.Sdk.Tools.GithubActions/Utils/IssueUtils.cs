using System;
using System.Collections.Generic;
using System.Text;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using Octokit;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    public class IssueUtils
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
        /// information from the input PullRequest
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
        /// Common function to add a comment to an Issue or PullRequest. Underneath the scenes
        /// Issues and PullRequests are the same when it comes to creating comments directly on
        /// them.
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="repositoryId"></param>
        /// <param name="issueOrPullRequestNumber"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        internal static async Task CreateComment(GitHubClient gitHubClient, 
                                                 long repositoryId, 
                                                 int issueOrPullRequestNumber, 
                                                 string comment)
        {
            await gitHubClient.Issue.Comment.Create(repositoryId, issueOrPullRequestNumber, comment);
        }
    }
}
