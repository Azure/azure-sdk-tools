using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor
{
    /// <summary>
    /// GitHubEventClient is a singleton. It holds the GitHubClient and Rules instances as well
    /// as any updates queued during event processing. After all the relevant rules have been processed, 
    /// a call to ProcessPendingUpdates will process all of the pending updates. This ensures that the 
    /// individual rules don't need to deal with calls to GitHub and the respective error processing, 
    /// within the rules, themselves.
    /// </summary>
    public class GitHubEventClient
    {
        // Exception string partial from the call to GitHubClient.Repository.Collaborator.ReviewPermission
        // used to determine if the call threw because the user being checked was a bot or didn't exist.
        private static readonly string NotAUserPartial = "is not a user";

        /// <summary>
        /// Class to store the information needed to create a GitHub Comment on an Issue or PullRequest.
        /// </summary>
        public class GitHubComment
        {
            private long _repositoryId;
            private int _issueOrPullRequestNumber;
            private string _comment;

            public long RepositoryId { get { return _repositoryId; } }
            public int IssueOrPullRequestNumber { get { return _issueOrPullRequestNumber; } }
            public string Comment { get { return _comment; } }

            public GitHubComment(long repositoryId, int issueOrPullRequestNumder, string comment) 
            { 
                _repositoryId = repositoryId;
                _issueOrPullRequestNumber = issueOrPullRequestNumder;
                _comment = comment;
            }
        }

        /// <summary>
        /// Class to store the information needed to dismiss a PullRequest review 
        /// </summary>
        public class GitHubReviewDismissal
        {
            private long _repositoryId;
            private int _pullRequestNumber;
            private long _reviewId;
            private string _dismissalMessage;

            public long RepositoryId { get { return _repositoryId; } }
            public int PullRequestNumber { get { return _pullRequestNumber; } }
            public long ReviewId { get { return _reviewId; } }
            public string DismissalMessage { get { return _dismissalMessage; } }

            public GitHubReviewDismissal(long repositoryId, int pullRequestNumber, long reviewId, string dismissalMessage)
            {
                _repositoryId = repositoryId;
                _pullRequestNumber = pullRequestNumber;
                _reviewId = reviewId;
                _dismissalMessage = dismissalMessage;
            }
        }

        /// <summary>
        /// Class to store the information needed to lock an Issue
        /// </summary>
        public class GitHubIssueToLock
        {
            private long _repositoryId;
            private int _issueNumber;
            private LockReason _lockReason;

            public long RepositoryId { get { return _repositoryId; } }
            public int IssueNumber { get { return _issueNumber; } }
            public LockReason LockReason { get { return _lockReason; } }

            public GitHubIssueToLock(long repositoryId, int issueNumber, LockReason lockReason)
            {
                _repositoryId = repositoryId;
                _issueNumber = issueNumber;
                _lockReason = lockReason;
            }
        }

        /// <summary>
        /// Used by scheduled/cron event processing which processes multiple Issues or PullRequests. This
        /// stores the IssueUpdate and the information necessary to update the Issue or PullRequest.
        /// </summary>
        public class GitHubIssueToUpdate
        {
            private long _repositoryId;
            private int _issueOrPRNumber;
            private IssueUpdate _issueUpdate;

            public long RepositoryId { get { return _repositoryId; } }
            public int IssueOrPRNumber { get { return _issueOrPRNumber; } }
            public IssueUpdate IssueUpdate { get { return _issueUpdate; } }

            public GitHubIssueToUpdate(long repositoryId, int issueOrPRNumber, IssueUpdate issueUpdate)
            {
                _repositoryId = repositoryId;
                _issueOrPRNumber = issueOrPRNumber;
                _issueUpdate = issueUpdate;
            }
        }


        private GitHubClient _gitHubClient = null;
        private RulesConfiguration _rulesConfiguration = null;
        // Protected instead of private so the mock class can access them
        protected IssueUpdate _issueUpdate = null;
        protected List<GitHubComment> _gitHubComments = new List<GitHubComment>();
        protected List<GitHubReviewDismissal> _gitHubReviewDismissals = new List<GitHubReviewDismissal>();
        // Locking issues is only done through scheduled event processing
        protected List<GitHubIssueToLock> _gitHubIssuesToLock = new List<GitHubIssueToLock>();
        // Scheduled event processing can process multiple issues, this list will not be used
        // for action processing which uses a shared event.
        protected List<GitHubIssueToUpdate> _gitHubIssuesToUpdate = new List<GitHubIssueToUpdate>();

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
        /// <param name="issueOrPullRequestNumber">The Issue or PullRequest number if not processing a scheduled task.</param>
        /// <returns>Integer, the number of update calls made</returns>
        public virtual async Task<int> ProcessPendingUpdates(long repositoryId, int issueOrPullRequestNumber = 0)
        {
            Console.WriteLine("Processing pending updates...");
            int numUpdates = 0;
            int numExpectedUpdates = ComputeNumberOfExpectedUpdates();
            try
            {
                // Process the issue update
                if (_issueUpdate != null)
                {
                    await _gitHubClient.Issue.Update(repositoryId, 
                                                     issueOrPullRequestNumber, 
                                                     _issueUpdate);
                    numUpdates++;
                }

                // Process any comments
                foreach (var comment in _gitHubComments)
                {
                    await _gitHubClient.Issue.Comment.Create(comment.RepositoryId,
                                                             comment.IssueOrPullRequestNumber,
                                                             comment.Comment);
                    numUpdates++;
                }

                // Process any PullRequest review dismissals
                foreach (var dismissal in _gitHubReviewDismissals)
                {
                    var prReview = new PullRequestReviewDismiss();
                    prReview.Message = dismissal.DismissalMessage;
                    await _gitHubClient.PullRequest.Review.Dismiss(dismissal.RepositoryId,
                                                                   dismissal.PullRequestNumber,
                                                                   dismissal.ReviewId,
                                                                   prReview);
                    numUpdates++;
                }

                // Process any issue locks
                foreach (var issueToLock in _gitHubIssuesToLock)
                {
                    await _gitHubClient.Issue.LockUnlock.Lock(issueToLock.RepositoryId,
                                                              issueToLock.IssueNumber,
                                                              issueToLock.LockReason);
                    numUpdates++;
                }

                // Process any Scheduled task IssueUpdates
                foreach (var issueToUpdate in _gitHubIssuesToUpdate)
                {
                    await _gitHubClient.Issue.Update(issueToUpdate.RepositoryId, 
                                                     issueToUpdate.IssueOrPRNumber, 
                                                     issueToUpdate.IssueUpdate);
                    numUpdates++;
                }
                Console.WriteLine("Finished processing pending updates.");
            }
            // For the moment, nothing special is being done when rate limit exceptions are
            // thrown but keep them separate in case that changes.
            catch (RateLimitExceededException rateLimitEx)
            {
                string message = $"RateLimitExceededException was thrown processing pending updates. Total expected updates={numExpectedUpdates}, number of updates made={numUpdates}.";
                Console.WriteLine(message);
                Console.WriteLine(rateLimitEx);
            }
            catch (SecondaryRateLimitExceededException secondaryRateLimitEx)
            {
                string message = $"SecondaryRateLimitExceededException was thrown processing pending updates. Total expected updates={numExpectedUpdates}, number of updates made={numUpdates}.";
                Console.WriteLine(message);
                Console.WriteLine(secondaryRateLimitEx);
            }
            catch (Exception ex)
            {
                string message = $"Exception was thrown processing pending updates. Total expected updates={numExpectedUpdates}, number of updates made={numUpdates}.";
                Console.WriteLine(message);
                Console.WriteLine(ex);
            }

            return numUpdates;
        }

        /// <summary>
        /// Compute and output the number of expected updates.
        /// </summary>
        /// <returns>int, the total number of expected updates</returns>
        public int ComputeNumberOfExpectedUpdates()
        {
            int numUpdates = 0;
            if (_issueUpdate != null)
            {
                Console.WriteLine("Common IssueUpdate from rules processing will be updated.");
                numUpdates++;
            }
            if (_gitHubComments.Count > 0)
            {
                Console.WriteLine($"Number of Comments to create {_gitHubComments.Count}");
                numUpdates += _gitHubComments.Count;
            }
            if (_gitHubReviewDismissals.Count > 0)
            {
                Console.WriteLine($"Number of Review Dismissals {_gitHubReviewDismissals.Count}");
                numUpdates += _gitHubReviewDismissals.Count;
            }
            if (_gitHubIssuesToLock.Count > 0)
            {
                Console.WriteLine($"Number of Issues to Lock {_gitHubIssuesToLock.Count}");
                numUpdates += _gitHubIssuesToLock.Count;
            }
            if (_gitHubIssuesToUpdate.Count > 0)
            {
                Console.WriteLine($"Number of IssuesUpdates (only applicable for Scheduled events) {_gitHubIssuesToUpdate.Count}");
                numUpdates += _gitHubIssuesToUpdate.Count;
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
            // Get the Minutes till reset.
            TimeSpan span = miscRateLimit.Resources.Core.Reset.UtcDateTime.Subtract(DateTime.UtcNow);
            // In the message, cast TotalMinutes to an int to get a whole number of minutes.
            string rateLimitMessage = $"Limit={miscRateLimit.Resources.Core.Limit}, Remaining={miscRateLimit.Resources.Core.Remaining}, Limit Reset in {(int)span.TotalMinutes} minutes.";
            if (prependMessage != null)
            {
                rateLimitMessage = $"{prependMessage} {rateLimitMessage}";
            }
            Console.WriteLine(rateLimitMessage);
        }

        /// <summary>
        /// Write the current rate limit and remaining number of transactions.
        /// </summary>
        /// <param name="prependMessage">Optional message to prepend to the rate limit message.</param>
        /// <returns></returns>
        public async Task WriteSearchRateLimits(string prependMessage = null)
        {
            var miscRateLimit = await GetRateLimits();
            // Get the Seconds till reset. Unlike the core rate limit which resets every hour, the search rate limit
            // should reset every minute.
            TimeSpan span = miscRateLimit.Resources.Search.Reset.UtcDateTime.Subtract(DateTime.UtcNow);
            // In the message, cast TotalSeconds to an int to get a whole number of minutes.
            string rateLimitMessage = $"Search Limit={miscRateLimit.Resources.Search.Limit}, Remaining={miscRateLimit.Resources.Search.Remaining}, Limit Reset in {(int)span.TotalSeconds} seconds.";
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
            return await _gitHubClient.RateLimit.GetRateLimits();
        }

        /// <summary>
        /// Overloaded convenience function that'll return the IssueUpdate. Actions all make changes to
        /// the same, shared, IssueUpdate because they're processing on the same event. For scheduled 
        /// event processing, there will be multiple, unique IssueUpdates and there won't be a shared one.
        /// </summary>
        /// <param name="issue">Octokit.Issue from the event payload</param>
        /// <param name="isProcessingAction">Whether or not actions are being processed. Default is true.</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(Issue issue, bool isProcessingAction = true)
        {
            if (isProcessingAction)
            {
                if (null == _issueUpdate)
                {
                    _issueUpdate = issue.ToUpdate();
                }
                return _issueUpdate;
            }
            else
            {
                return issue.ToUpdate();
            }
        }

        /// <summary>
        /// Overloaded convenience function that'll return the IssueUpdate. Actions all make changes to
        /// the same, shared, IssueUpdate because they're processing on the same event. For scheduled 
        /// event processing, there will be multiple, unique IssueUpdates and there won't be shared one.
        /// </summary>
        /// <param name="pullRequest">Octokit.PullRequest from the event payload</param>
        /// <param name="isProcessingAction">Whether or not actions are being processed. Default is true.</param>
        /// <returns>Octokit.IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate(PullRequest pullRequest, bool isProcessingAction = true)
        {
            if (isProcessingAction)
            {
                if (null == _issueUpdate)
                {
                    _issueUpdate = CreateIssueUpdateForPR(pullRequest);
                }
                return _issueUpdate;
            }
            else
            {
                return CreateIssueUpdateForPR(pullRequest);
            }
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
            _gitHubComments.Add(gitHubComment);
        }

        /// <summary>
        /// Get all the reviews for a given pull request.
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="pullRequestNumber">The pull request number</param>
        /// <returns>IReadOnlyList of PullRequestReview</returns>
        public virtual async Task<IReadOnlyList<PullRequestReview>> GetReviewsForPullRequest(long repositoryId, int pullRequestNumber)
        {
            return await _gitHubClient.PullRequest.Review.GetAll(repositoryId, pullRequestNumber);
        }

        public void DismissReview(long repositoryId, int pullRequestNumber, long reviewId, string dismissalMessage)
        {
            GitHubReviewDismissal gitHubReviewDismissal = new GitHubReviewDismissal(repositoryId, 
                                                                                    pullRequestNumber, 
                                                                                    reviewId, 
                                                                                    dismissalMessage);
            _gitHubReviewDismissals.Add(gitHubReviewDismissal);
        }

        /// <summary>
        /// Create a GitHubIssueToLock and add it to the list of Issues to lock which gets
        /// gets updated with the pending updates.
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="issueNumber"></param>
        /// <param name="lockReason"></param>
        public void LockIssue(long repositoryId, int issueNumber, LockReason lockReason)
        {
            GitHubIssueToLock gitHubIssueToLock = new GitHubIssueToLock(repositoryId,
                                                                        issueNumber,
                                                                        lockReason);
            _gitHubIssuesToLock.Add(gitHubIssueToLock);
        }

        /// <summary>
        /// Scheduled events will process multiple issue update. This function adds them to
        /// list of IssueUpdates that will get processed with the pending updates.
        /// </summary>
        /// <param name="repositoryId">Repository Id of the Issue or PullRequest</param>
        /// <param name="issueOrPRNumber">Issue or PullRequest number being updated</param>
        /// <param name="issueUpdate">The modified IssueUpdate</param>
        public void AddToIssueUpdateList(long repositoryId, int issueOrPRNumber, IssueUpdate issueUpdate)
        {
            GitHubIssueToUpdate gitHubIssueToUpdate = new GitHubIssueToUpdate(repositoryId, issueOrPRNumber, issueUpdate);
            _gitHubIssuesToUpdate.Add(gitHubIssueToUpdate);
        }


        /// <summary>
        /// Common function to get files for a pull request. The default page size for the API is 30
        /// and needs to be set to 100 to minimize calls, do that here.
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="pullRequestNumber">The pull request number</param>
        /// <returns>IReadOnlyList of PullRequestFiles associated with the pull request</returns>
        public virtual async Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // For whatever reason the default page size is 30 instead of 100.
            ApiOptions apiOptions = new ApiOptions();
            apiOptions.PageSize = 100;
            return await _gitHubClient.PullRequest.Files(repositoryId, pullRequestNumber, apiOptions);
        }

        /// <summary>
        /// Check to see if a given user is a Collaborator
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, true if the user is a Collaborator, false otherwise</returns>
        public virtual async Task<bool> IsUserCollaborator(long repositoryId, string user)
        {
            return await _gitHubClient.Repository.Collaborator.IsCollaborator(repositoryId, user);
        }

        /// <summary>
        /// Check to see if the user is a member of the given Org
        /// </summary>
        /// <param name="orgName">Organization name. Chances are this will only ever be "Azure"</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, true if the user is a member of the org, false otherwise</returns>
        public virtual async Task<bool> IsUserMemberOfOrg(string orgName, string user)
        {
            // Chances are the orgname is only going to be "Azure"
            return await _gitHubClient.Organization.Member.CheckMember(orgName, user);
        }

        /// <summary>
        /// Check whether or not a user has a specific collaborator permission
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <param name="permission">OctoKit.PermissionLevel to check</param>
        /// <returns>bool, true if the user has the permission level, false otherwise</returns>
        public async Task<bool> DoesUserHavePermission(long repositoryId, string user, string permission)
        {
            List<string> permissionList = new List<string>
            {
                permission
            };
            return await DoesUserHavePermissions(repositoryId, user, permissionList);
        }

        /// <summary>
        /// There are a lot of checks to see if user has Write Collaborator permissions, however,
        /// Collaborator permission levels are Admin, Write, Read and None. Checking to see if a
        /// user has Write permissions translates to does the user have Admin or Write.
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, true if the use has Write or Admin permissions, false otherwise</returns>
        public async Task<bool> DoesUserHaveAdminOrWritePermission(long repositoryId, string user)
        {
            List<string> permissionList = new List<string>
            {
                PermissionLevel.Admin,
                PermissionLevel.Write
            };
            return await DoesUserHavePermissions(repositoryId, user, permissionList);
        }

        /// <summary>
        /// Check whether or not the user has one of the permissions in the list. There's no concept of a permission
        /// hierarchy when checking permissions. For example, if something requires a user have Write permission
        /// then the check needs to look for Write or Admin permission.
        /// </summary>
        /// <param name="repositoryId">The Id of the Repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <param name="permissionList">List of Octokit.PermissionLevels</param>
        /// <returns>bool, true if the user has any permissions in the permissionList, false otherwise</returns>
        public virtual async Task<bool> DoesUserHavePermissions(long repositoryId, string user, List<string> permissionList)
        {
            try
            {
                CollaboratorPermissionResponse collaboratorPermission = await _gitHubClient.Repository.Collaborator.ReviewPermission(repositoryId, user);
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
        /// <returns>SearchIssuesRequest created with the information passed in.</returns>
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
        /// <returns>OctoKit.SearchIssuesResult</returns>
        public virtual async Task<SearchIssuesResult> QueryIssues(SearchIssuesRequest searchIssuesRequest)
        {
            int maxTries = 3;
            // 90 seconds
            int sleepDuration = 90000;
            for (int tryNumber = 1; tryNumber <= maxTries; tryNumber++)
            {
                try
                {
                    Console.WriteLine($"Calling SearchIssues, try number {tryNumber}. Page number={searchIssuesRequest.Page}, results per page={searchIssuesRequest.PerPage}");
                    await WriteSearchRateLimits("Search RateLimit before call to SearchIssues:");
                    var searchIssueResult = await _gitHubClient.Search.SearchIssues(searchIssuesRequest);
                    await WriteSearchRateLimits("Search RateLimit after call to SearchIssues:");
                    Console.WriteLine($"Call returned {searchIssueResult.Items.Count} results out of {searchIssueResult.TotalCount} total results.");
                    return searchIssueResult;
                }
                catch (SecondaryRateLimitExceededException secondaryRateLimitEx)
                {
                    Console.WriteLine($"In QueryIssues, a SecondaryRateLimitExceededException was caught from a SearchIssues call.");
                    if (null != secondaryRateLimitEx.HttpResponse)
                    {
                        Console.WriteLine($"HttpStatusCode={secondaryRateLimitEx.HttpResponse.StatusCode}");
                        Console.WriteLine("HttpResponse info:");
                        foreach (KeyValuePair<string, string> kvp in secondaryRateLimitEx.HttpResponse.Headers)
                        {
                            Console.WriteLine($"[{kvp.Key}, {kvp.Value}]");
                        }
                    }
                    else
                    {
                        Console.WriteLine("secondaryRateLimitEx.HttpResponse was null");
                    }
                    if (tryNumber == maxTries)
                    {
                        Console.WriteLine($"QueryIssues, number of retries, {maxTries}, have been exhausted, rethrowing.");
                        throw;
                    }
                    else
                    {
                        Console.WriteLine($"QueryIssues, sleeping for {sleepDuration/1000} seconds before retrying.");
                        System.Threading.Thread.Sleep(sleepDuration);
                    }
                }
            }
            // This code will never get hit.
            // This is fix CS0161 (not all code paths return a value). Either the function will return a successful
            // SearchIssuesResult above OR it'll rethrow the last SecondaryRateLimitExceededException encountered
            // in the retry loop.
            SearchIssuesResult searchIssuesResult = new SearchIssuesResult();
            return searchIssuesResult;
        }

        /// <summary>
        /// This method creates a GitHubClient using the GITHUB_TOKEN from the environment for authentication
        /// </summary>
        /// <param name="productHeaderName">This is used to generate the User Agent string sent with each request. The name used should represent the product, the GitHub Organization, or the GitHub username that's using Octokit.net (in that order of preference).</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">If the product header name is null or empty</exception>
        /// <exception cref="ApplicationException">If there is no GITHUB_TOKEN in the environment</exception>
        /// <returns>Authenticated GitHubClient</returns>
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
        /// <returns>RulesConfiguration loaded from the input location or well known location</returns>
        public virtual RulesConfiguration LoadRulesConfiguration(string rulesConfigLocation = null)
        {
            // if the rulesConfigLocation is set, try and load the rules from there, otherwise
            // use the directory climber to find the root of the repository and pull it from
            // the .github or .github/workflows directory
            var rulesConfiguration = new RulesConfiguration(rulesConfigLocation);
            return rulesConfiguration;
        }

        /// <summary>
        /// Class to hold the response from the AI Label Service query
        /// </summary>
        public class LabelResponse
        {
            public string[] Labels { get; set; }
        }

        public virtual async Task<List<string>> QueryAILabelService(IssueEventGitHubPayload issueEventPayload)
        {
            // The LABEL_SERVICE_API_KEY is queried from Keyvault as part of the action and added to the
            // environment.
            string AIServiceKey = Environment.GetEnvironmentVariable("LABEL_SERVICE_API_KEY");
            if (string.IsNullOrEmpty(AIServiceKey))
            {
                Console.WriteLine("LABEL_SERVICE_API_KEY is null or empty.");
                return new List<string>();
            }
            string requestUrl = $"https://issuelabeler.azurewebsites.net/api/AzureSdkIssueLabelerService?code={AIServiceKey}";

            var payload = new
            {
                IssueNumber = issueEventPayload.Issue.Number,
                issueEventPayload.Issue.Title,
                issueEventPayload.Issue.Body,
                IssueUserLogin = issueEventPayload.Issue.User.Login,
                RepositoryName = issueEventPayload.Repository.Name,
                RepositoryOwnerName = issueEventPayload.Repository.Owner.Login
            };
            using var client = new HttpClient();
            List<string> returnList;
            try
            {
                var response = await client.PostAsJsonAsync(requestUrl, payload).ConfigureAwait(false);
                // The AI Label Service will return a HttpStatusCode.OK in the following cases
                // 1. There is a AI model for the repository. It'll also return the list of labels, if any.
                // 2. There is not an AI model for the repository. The list of suggestions will be empty. At
                //    this point the expectation is that the logging for the AI label service will indicate
                //    that it was called for a repository that doesn't have AI models.
                // If the AI Label Service doesn't return HttpStatusCode.OK, just log that here and return an
                // empty list.
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var suggestions = await response.Content.ReadFromJsonAsync<LabelResponse>().ConfigureAwait(false);
                    returnList = new List<string>(suggestions.Labels);
                }
                else
                {
                    Console.WriteLine($"The AI Label service did not return a success. Status Code={response.StatusCode}, Reason={response.ReasonPhrase}");
                    returnList = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception calling AI Label Service. Exception={ex}");
                returnList = new List<string>();
            }
            return returnList;
        }
    }
}
