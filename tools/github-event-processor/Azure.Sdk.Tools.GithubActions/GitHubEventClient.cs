using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    // Class to store the GitHubClient and Rules instances
    // This class will also store the messages so we can do updates
    // at the end of processing instead of only the IssueUpdate being at the end
    public class GitHubEventClient
    {
        internal class GitHubComment
        {
            private long _repositoryId;
            private int _issueOrPullRequestNumber;
            private string _comment;

            public long RepositoryId { get { return this._repositoryId; } }
            public int IssueOrPullRequestNumber { get { return this._issueOrPullRequestNumber; } }
            public string Comment { get { return this._comment; } }

            public GitHubComment(long repositoryId, int issueOrPullRequestNumder, string comment) 
            { 
                this._repositoryId = repositoryId;
                this._issueOrPullRequestNumber = issueOrPullRequestNumder;
                this._comment = comment;
            }
        }

        internal class GitHubReviewDismissal
        {
            private long _repositoryId;
            private int _pullRequestNumber;
            private long _reviewId;
            private string _dismissalMessage;

            public long RepositoryId { get { return this._repositoryId; } }
            public int PullRequestNumber { get { return this._pullRequestNumber; } }
            public long ReviewId { get { return this._reviewId; } }
            public string DismissalMessage { get { return this._dismissalMessage; } }

            public GitHubReviewDismissal(long repositoryId, int pullRequestNumber, long reviewId, string dismissalMessage)
            {
                this._repositoryId = repositoryId;
                this._pullRequestNumber = pullRequestNumber;
                this._reviewId = reviewId;
                this._dismissalMessage = dismissalMessage;
            }
        }

        private GitHubClient _gitHubClient = null;
        private RulesConfiguration _rulesConfiguration = null;
        private IssueUpdate _issueUpdate = null;
        private List<GitHubComment> _gitHubComments = new List<GitHubComment>();
        private List<GitHubReviewDismissal> _gitHubReviewDismissals = new List<GitHubReviewDismissal>();

        public virtual RulesConfiguration RulesConfiguration
        {
            get { return _rulesConfiguration; }
        }
        public GitHubEventClient(string productHeaderName, string rulesConfigLocation = null)
        {
            _gitHubClient = createClientWithGitHubEnvToken(productHeaderName);
            _rulesConfiguration = loadRulesConfiguration(rulesConfigLocation);
        }

        /// <summary>
        /// Process any of the pending updates stored on this class. Right now that consists of the following:
        /// 1. IssueUpdate
        /// 2. Added Comments
        /// 3. Removed Dismissals
        /// </summary>
        /// <returns>Integer, the number of update calls made</returns>
        public virtual async Task<int> ProcessPendingUpdates(long repositoryId, int issueOrPullRequestNumber)
        {
            int numUpdates = 0;

            // Process the issue update
            if (this._issueUpdate != null) 
            {
                numUpdates++;
                try
                {
                    await this._gitHubClient.Issue.Update(repositoryId, issueOrPullRequestNumber, this._issueUpdate);
                }
                catch (Exception ex)
                {
                    // JRS - what to do if this throws?
                    Console.WriteLine(ex);
                }
            }

            // Process any comments
            foreach (var comment in this._gitHubComments)
            {
                numUpdates++;
                try
                {
                    await this._gitHubClient.Issue.Comment.Create(comment.RepositoryId,
                                                                  comment.IssueOrPullRequestNumber,
                                                                  comment.Comment);
                }
                catch (Exception ex)
                {
                    // JRS - what to do if this throws?
                    Console.WriteLine(ex);
                }
            }

            foreach (var dismissal in this._gitHubReviewDismissals)
            {
                numUpdates++;
                try
                {
                    var prReview = new PullRequestReviewDismiss();
                    prReview.Message = dismissal.DismissalMessage;
                    await this._gitHubClient.PullRequest.Review.Dismiss(dismissal.RepositoryId,
                                                                        dismissal.PullRequestNumber,
                                                                        dismissal.ReviewId,
                                                                        prReview);
                }
                catch (Exception ex)
                {
                    // JRS - what to do if this throws?
                    Console.WriteLine(ex);
                }
            }

            return numUpdates;
        }

        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate.
        /// </summary>
        /// <param name="issue">Octokit.Issue from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(Issue issue, IssueUpdate issueUpdate)
        {
            if (null == issueUpdate)
            {
                _issueUpdate = issue.ToUpdate();
            }
            return _issueUpdate;
        }

        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate. 
        /// </summary>
        /// <param name="pullRequest">Octokit.PullRequest from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(PullRequest pullRequest, IssueUpdate issueUpdate)
        {
            if (null == issueUpdate)
            {
                _issueUpdate = CreateIssueUpdateForPR(pullRequest);
            }
            return _issueUpdate;
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
        internal IssueUpdate CreateIssueUpdateForPR(PullRequest pullRequest)
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
                State = pullRequest.State.TryParse(out state) ? (ItemState?)state : null,
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
        /// Common function to get files for a pull request. The default page size for the API is 30
        /// and needs to be set to 100 to minimize calls, do that here.
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="repositoryId"></param>
        /// <param name="pullRequestNumber"></param>
        /// <returns></returns>
        internal async Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // For whatever reason the default page size
            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            return await this._gitHubClient.PullRequest.Files(repositoryId, pullRequestNumber, apiOptions);
        }


        /// <summary>
        /// This method creates a GitHubClient using the GITHUB_TOKEN from the environment for authentication
        /// </summary>
        /// <param name="productHeaderName">This is used to generate the User Agent string sent with each request. The name used should represent the product, the GitHub Organization, or the GitHub username that's using Octokit.net (in that order of preference).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ApplicationException"></exception>
        internal GitHubClient createClientWithGitHubEnvToken(string productHeaderName)
        {
            if (string.IsNullOrEmpty(productHeaderName))
            {
                throw new ArgumentException("productHeaderName cannot be null or empty");
            }
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(githubToken))
            {
                throw new ApplicationException("GITHUB_TOKEN cannot be null or empty");
            }
            var gitHubClient = new GitHubClient(new ProductHeaderValue(productHeaderName))
            {
                Credentials = new Credentials(githubToken)
            };
            return gitHubClient;
        }

        /// <summary>
        /// Load the rules configuration.
        /// </summary>
        /// <param name="rulesConfigLocation">Optional path to the rules config location. If not set it'll check for the rules configuration in its well known location.</param>
        /// <returns></returns>
        internal RulesConfiguration loadRulesConfiguration(string rulesConfigLocation = null)
        {
            // if the rulesConfigLocation is set, try and load the rules from there, otherwise
            // use the directory climber to find the root of the repository and pull it from
            // the .github or .github/workflows directory
            var rulesConfiguration = new RulesConfiguration();
            return rulesConfiguration;
        }
    }
}
