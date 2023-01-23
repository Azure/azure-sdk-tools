using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    // Class to store the GitHubClient and Rules instances
    // This class will also store the messages so we can do updates
    // at the end of processing instead of only the IssueUpdate being at the end
    public class GitHubEventClient
    {
        private static readonly string NotAUserPartial = "is not a user";
        public class GitHubComment
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

        public class GitHubReviewDismissal
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
        protected IssueUpdate _issueUpdate = null;
        protected List<GitHubComment> _gitHubComments = new List<GitHubComment>();
        protected List<GitHubReviewDismissal> _gitHubReviewDismissals = new List<GitHubReviewDismissal>();

        public RulesConfiguration RulesConfiguration
        {
            get { return _rulesConfiguration; }
        }

        public GitHubEventClient(string productHeaderName, string rulesConfigLocation = null)
        {
            _gitHubClient = CreateClientWithGitHubEnvToken(productHeaderName);
            _rulesConfiguration = LoadRulesConfiguration(rulesConfigLocation);
        }

        /// <summary>
        /// Process any of the pending updates stored on this class. Right now that consists of the following:
        /// 1. IssueUpdate
        /// 2. Added Comments
        /// 3. Removed Dismissals
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="issueOrPullRequestNumber">The Issue or PullRequest number</param>
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
        /// Write the current rate limit and remaining number of transactions.
        /// </summary>
        /// <param name="prependMessage">Optional message to prepend to the rate limit message.</param>
        /// <returns></returns>
        public async Task WriteRateLimits(string prependMessage = null)
        {
            var miscRateLimit = await GetRateLimits();
            string rateLimitMessage = $"Limit={miscRateLimit.Resources.Core.Limit}, Remaining={miscRateLimit.Resources.Core.Remaining}";
            if (prependMessage != null)
            {
                rateLimitMessage = $"{prependMessage} {rateLimitMessage}";
            }
            Console.WriteLine(rateLimitMessage);
        }

        /// <summary>
        /// Using the authenticated GitHubClient, call the RateLimit API to get the rate limits.
        /// </summary>
        /// <returns>Octokit.MiscellaneousRateLimit which contains the rate limit information.</returns>
        public async Task<MiscellaneousRateLimit> GetRateLimits()
        {
            return await this._gitHubClient.RateLimit.GetRateLimits();
        }

        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate.
        /// </summary>
        /// <param name="issue">Octokit.Issue from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(Issue issue)
        {
            if (null == this._issueUpdate)
            {
                this._issueUpdate = issue.ToUpdate();
            }
            return this._issueUpdate;
        }

        /// <summary>
        /// Overloaded convenience function that'll return the existing IssueUpdate, if non-null, and
        /// create one to return, if null. This prevents the same code from being in every function
        /// that needs an IssueUpdate. 
        /// </summary>
        /// <param name="pullRequest">Octokit.PullRequest from the event payload</param>
        /// <param name="issueUpdate">Octokit.IssueUpdate that'll be returned if non-null</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(PullRequest pullRequest)
        {
            if (null == this._issueUpdate)
            {
                this._issueUpdate = CreateIssueUpdateForPR(pullRequest);
            }
            return this._issueUpdate;
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
        /// Create a comment that will be added to the PR with the pending updates
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="issueOrPullRequestNumber">The Issue or PullRequest number</param>
        /// <param name="comment">The comment being created.</param>
        /// <returns></returns>
        public void CreateComment(long repositoryId, int issueOrPullRequestNumber, string comment)
        {
            GitHubComment gitHubComment = new GitHubComment(repositoryId, issueOrPullRequestNumber, comment);
            this._gitHubComments.Add(gitHubComment);
        }

        /// <summary>
        /// Get all the reviews for a given pull request.
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="pullRequestNumber">The pull request number</param>
        /// <returns></returns>
        public virtual async Task<IReadOnlyList<PullRequestReview>> GetReviewsForPullRequest(long repositoryId, int pullRequestNumber)
        {
            return await this._gitHubClient.PullRequest.Review.GetAll(repositoryId, pullRequestNumber);
        }

        public void DismissReview(long repositoryId, int pullRequestNumber, long reviewId, string dismissalMessage)
        {
            GitHubReviewDismissal gitHubReviewDismissal = new GitHubReviewDismissal(repositoryId, 
                                                                                    pullRequestNumber, 
                                                                                    reviewId, 
                                                                                    dismissalMessage);
            this._gitHubReviewDismissals.Add(gitHubReviewDismissal);
        }


        /// <summary>
        /// Common function to get files for a pull request. The default page size for the API is 30
        /// and needs to be set to 100 to minimize calls, do that here.
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="pullRequestNumber">The pull request number</param>
        /// <returns></returns>
        public virtual async Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // For whatever reason the default page size is 30 instead of 100.
            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            return await this._gitHubClient.PullRequest.Files(repositoryId, pullRequestNumber, apiOptions);
        }

        /// <summary>
        /// Check to see if a given user is a Collaborator
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns></returns>
        public virtual async Task<bool> IsUserCollaborator(long repositoryId, string user)
        {
            return await this._gitHubClient.Repository.Collaborator.IsCollaborator(repositoryId, user);
        }

        /// <summary>
        /// Check to see if the user is a member of the given Org
        /// </summary>
        /// <param name="orgName">Organization name. Chances are this will only ever be "Azure"</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns></returns>
        public virtual async Task<bool> IsUserMemberOfOrg(string orgName, string user)
        {
            // Chances are the orgname is only going to be "Azure"
            return await this._gitHubClient.Organization.Member.CheckMember(orgName, user);
        }

        /// <summary>
        /// Check whether or not a user has a specific collaborator permission
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <param name="permission">OctoKit.PermissionLevel to check</param>
        /// <returns></returns>
        public async Task<bool> DoesUserHavePermission(long repositoryId, string user, PermissionLevel permission)
        {
            List<PermissionLevel> permissionList = new List<PermissionLevel>
            {
                permission
            };
            return await DoesUserHavePermissions(repositoryId, user, permissionList);
        }

        /// <summary>
        /// There are a lot of checks to see if user has Write Collaborator permissions however permissions however
        /// Collaborator permissions levels are Admin, Write, Read and None. Checking to see if a user has Write
        /// permissions translates to does the user have Admin or Write.
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns></returns>
        public async Task<bool> DoesUserHaveAdminOrWritePermission(long repositoryId, string user)
        {
            List<PermissionLevel> permissionList = new List<PermissionLevel>
            {
                PermissionLevel.Admin,
                PermissionLevel.Write
            };
            return await DoesUserHavePermissions(repositoryId, user, permissionList);
        }


        // There are several checks that look to see if a user's permission is NOT Admin or Write which
        // means both need to be checked but making multiple calls is not necessary
        /// <summary>
        /// Check whether or not the user has one of the permissions in the list. There's no concept of a permission
        /// hierarchy when checking permissions. For example, if something requires a user have Write permission
        /// then the check needs to look for Write or Admin permission.
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <param name="permissionList">List of Octokit.PermissionLevels</param>
        /// <returns></returns>
        public virtual async Task<bool> DoesUserHavePermissions(long repositoryId, string user, List<PermissionLevel> permissionList)
        {
            try
            {
                CollaboratorPermission collaboratorPermission = await this._gitHubClient.Repository.Collaborator.ReviewPermission(repositoryId, user);
                // If the user has one of the permissions on the list return true
                foreach (var permission in permissionList)
                {
                    if (collaboratorPermission.Permission == permission)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // If this throws it's because it's being checked for a non-user (bot) or the user somehow doesn't exist.
                // If that's not the case, rethrow the exception, otherwise let processing return false
                if (!ex.Message.Contains(NotAUserPartial, StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }
            }
            return false;
        }

        /// <summary>
        /// Create a SearchIssuesRequest with the information passed in.
        /// </summary>
        /// <param name="repoOwner">Should be the repository.Owner.Login from the cron payload</param>
        /// <param name="repoName">Should be repository.Name from the cron payload</param>
        /// <param name="issueType">IssueTypeQualifier of Issue or PullRequest</param>
        /// <param name="itemState">ItemState of Open or Closed</param>
        /// <param name="issueIsQualifiers">Optional: List of IssueIsQualifier (ex. locked/unlocked) to include, null if none</param>
        /// <param name="labelsToInclude">Optional: List of labels to include, null if none</param>
        /// <param name="labelsToExclude">Optional: List of labels to exclude, null if none</param>
        /// <param name="daysSinceLastUpdate">Optional: Number of days since last updated </param>
        /// <returns>SearchIssuesRequest with the information passed in.</returns>
        public SearchIssuesRequest CreateSearchRequest(string repoOwner,
                                                       string repoName,
                                                       IssueTypeQualifier issueType,
                                                       ItemState itemState,
                                                       int daysSinceLastUpdate = 0,
                                                       List<IssueIsQualifier> issueIsQualifiers = null,
                                                       List<string> labelsToInclude = null,
                                                       List<string> labelsToExclude = null)
        {
            var request = new SearchIssuesRequest();

            // The repo owner 
            request.Repos.Add(repoOwner, repoName);

            // Can only search for opened or closed
            request.State = itemState;
            if (null != issueIsQualifiers)
            {
                request.Is = issueIsQualifiers;
            }

            // restrict the search to issues (IssueTypeQualifier.Issue)
            // or pull requests (IssueTypeQualifier.PullRequest)
            request.Type = issueType;

            if (daysSinceLastUpdate > 0)
            {
                // Octokit's DateRange wants a DateTimeOffset as other constructors are depricated
                // AddDays of 0-days to effectively subtract them.
                DateTime daysAgo = DateTime.UtcNow.AddDays(0 - daysSinceLastUpdate);
                DateTimeOffset daysAgoOffset = new DateTimeOffset(daysAgo);
                request.Updated = new DateRange(daysAgoOffset, SearchQualifierOperator.LessThan);
            }

            if (null != labelsToInclude)
            {
                request.Labels = labelsToInclude;
            }

            if (null != labelsToExclude)
            {
                // This is how things would get exluded. Anything that needs to be an exclusion
                // for the query needs added to a SearchIssuesRequestExclusions and then
                // the Exclusions on the request needs to be set to that.
                var exclusions = new SearchIssuesRequestExclusions();
                exclusions.Labels = labelsToExclude;
                request.Exclusions = exclusions;
            }
            return request;
        }

        /// <summary>
        /// Execute the query for a given SearchIssuesRequest. It was necessary to break up the SearchIssuesRequest
        /// and the query due to pagination. The SearchIssuesResult will only contain to up the first 100 results.
        /// Subsequent results need to be requeried with the SearchIssuesRequest.Page incremented to get the next 100
        /// results and so on.
        /// </summary>
        /// <param name="searchIssuesRequest">SearchIssuesRequest objected which contains the search criteria.</param>
        /// <returns></returns>
        public virtual async Task<SearchIssuesResult> QueryIssues(SearchIssuesRequest searchIssuesRequest)
        {
            var searchIssueResult = await this._gitHubClient.Search.SearchIssues(searchIssuesRequest);
            return searchIssueResult;
        }

        /// <summary>
        /// This method creates a GitHubClient using the GITHUB_TOKEN from the environment for authentication
        /// </summary>
        /// <param name="productHeaderName">This is used to generate the User Agent string sent with each request. The name used should represent the product, the GitHub Organization, or the GitHub username that's using Octokit.net (in that order of preference).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public virtual GitHubClient CreateClientWithGitHubEnvToken(string productHeaderName)
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
        public virtual RulesConfiguration LoadRulesConfiguration(string rulesConfigLocation = null)
        {
            // if the rulesConfigLocation is set, try and load the rules from there, otherwise
            // use the directory climber to find the root of the repository and pull it from
            // the .github or .github/workflows directory
            var rulesConfiguration = new RulesConfiguration(rulesConfigLocation);
            return rulesConfiguration;
        }
    }
}
