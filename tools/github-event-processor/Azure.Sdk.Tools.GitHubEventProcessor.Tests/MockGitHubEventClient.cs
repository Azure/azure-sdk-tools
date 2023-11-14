using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests
{
    public class MockGitHubEventClient: GitHubEventClient
    {
        /// <summary>
        /// IsCollaborator return value. Defaults to false.
        /// </summary>
        public bool IsCollaboratorReturn { get; set; } = false;

        /// <summary>
        /// DoesUserHavePermissions return value. Defaults to false.
        /// </summary>
        public bool UserHasPermissionsReturn { get; set; } = false;

        /// <summary>
        /// OwnerCanBeAssignedToIssueInRepo will check the list to see if the owner is in there
        /// returning true if so, false otherwise
        /// </summary>
        public List<string> OwnersWithAssignPermission { get; set; } = new List<string>();

        /// <summary>
        /// IsUserMemberOfOrg value. Defaults to false.
        /// </summary>
        public bool IsUserMemberOfOrgReturn { get; set; } = false;

        public List<PullRequestReview> PullRequestReviews { get; set; } = new List<PullRequestReview>();
        public List<PullRequestFile> PullRequestFiles { get; set; } = new List<PullRequestFile>();

        public List<string> AILabelServiceReturn { get; set; } = new List<string>();

        public SearchIssuesResult SearchIssuesResultReturn { get; set; } = new SearchIssuesResult();

        public MockGitHubEventClient(string productHeaderName) : 
            base(productHeaderName)
        {

        }

        /// <summary>
        /// The mock ProcessPendingUpdates should just return the number of updates
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="issueOrPullRequestNumber"></param>
        /// <returns>integer,the number of pending updates that would be processed</returns>
        public override Task<int> ProcessPendingUpdates(long repositoryId, int issueOrPullRequestNumber = 0)
        {
            int numUpdates = 0;
            if (_issueUpdate != null)
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, Issue Update is non-null");
                numUpdates++;
            }
            else
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, Issue Update is null");
            }

            // The issue is being assigned. Note, this can only happen for issues, the issueOrPullRequestNumber
            // in this case will always be an issue with the way events are processed.
            if (_gitHubIssueAssignment != null)
            {
                Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, Issue Assignment is non-null. Assignees={string.Join(",", _gitHubIssueAssignment.Assignees)}");
                numUpdates++;
            }
            else
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, Issue Assignment is null");
            }

            if (_labelsToAdd.Count > 0)
            {
                Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of labels to add = {_labelsToAdd.Count} (only 1 call). Labels={string.Join(",", _labelsToAdd)}");
                // Adding labels is a single call to add them all
                numUpdates++;
            }

            if (_labelsToRemove.Count > 0)
            {
                Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of labels to remove = {_labelsToRemove.Count} (1 call for each). Labels={string.Join(",", _labelsToRemove)}");
            }
            else
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, number of labels to remove = 0");
            }
            // Removing labels is a call for each one being removed
            numUpdates += _labelsToRemove.Count;

            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of pending comments = {_gitHubComments.Count}");
            numUpdates += _gitHubComments.Count;

            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of pending dismissals = {_gitHubReviewDismissals.Count}");
            numUpdates += _gitHubReviewDismissals.Count;

            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of pending IssueUpdates = {_gitHubIssuesToUpdate.Count}");
            numUpdates += _gitHubIssuesToUpdate.Count;

            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of issues to Lock = {_gitHubIssuesToLock.Count}");
            numUpdates += _gitHubIssuesToLock.Count;

            return Task.FromResult(numUpdates);
        }

        /// <summary>
        /// IsUserCollaborator override. Returns IsCollaboratorReturn value
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, returns the IsCollaboratorReturn set for testing</returns>
        public override Task<bool> IsUserCollaborator(long repositoryId, string user)
        {
            return Task.FromResult(IsCollaboratorReturn);
        }

        /// <summary>
        /// IsUserMemberOfOrg override. Returns IsUserMemberOfOrgReturn value
        /// </summary>
        /// <param name="orgName">The organization name.</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <returns>bool, returns IsUserMemberOfOrgReturn set for testing</returns>
        public override Task<bool> IsUserMemberOfOrg(string orgName, string user)
        {
            return Task.FromResult(IsUserMemberOfOrgReturn);
        }


        /// <summary>
        /// DoesUserHavePermissions override. Returns UserHasPermissionsReturn value
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="user">The User.Login for the event object from the action payload</param>
        /// <param name="permissionList">The list of permissions to check for.</param>
        /// <returns>bool, returns UserHasPermissionsReturn for testing</returns>
        public override Task<bool> DoesUserHavePermissions(long repositoryId, string user, List<string> permissionList)
        {
            return Task.FromResult(UserHasPermissionsReturn);
        }


        /// <summary>
        /// OwnerCanBeAssignedToIssueInRepo override. Returns OwnerCanBeAssignedToIssueInRepoReturn value
        /// </summary>
        /// <param name="repoOwner">The owner of the repository. Repositories are in the form repoOwner/repoName. Azure/azure-sdk would have Azure as the owner and azure-sdk as the repo.</param>
        /// <param name="repoName">The repository name.</param>
        /// <param name="assignee">The potential assignee to check..</param>
        /// <returns>bool, returns OwnerCanBeAssignedToIssueInRepoReturn for testing</returns>
        public override Task<bool> OwnerCanBeAssignedToIssuesInRepo(string repoOwner, string repoName, string assignee)
        {
            return Task.FromResult(OwnersWithAssignPermission.Contains(assignee, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Mock call that will return a SearchIssuesResult for testing purposes
        /// </summary>
        /// <param name="searchIssuesRequest">SearchIssuesRequest objected which contains the search criteria.</param>
        /// <returns>OctoKit.SearchIssuesResult</returns>
        public override async Task<SearchIssuesResult> QueryIssues(SearchIssuesRequest searchIssuesRequest)
        {
            return await Task.FromResult(SearchIssuesResultReturn);
        }

        /// <summary>
        /// Create a bunch of fake issues for QueryIssues to return.
        /// </summary>
        /// <param name="numResults">The number of results to create.</param>
        public void CreateSearchIssuesResult(int numResults, Repository repository, ItemState itemState)
        {
            // Empty issues should be okay for the QueryResult. The scheduled events
            // never look at the returned issues because everything returned already
            // matches the search criteria. They only thing the rules are doing is
            // modifying the issues returned.
            List<Issue> issues = new List<Issue>();
            for(int iCounter=0;iCounter <numResults;iCounter++)
            {
                //
                User user = CreateFakeUser("FakeUser1");
                // public Issue(string url, string htmlUrl, string commentsUrl, string eventsUrl, int number, ItemState state, string title, string body, User closedBy, User user, IReadOnlyList<Label> labels, User assignee, IReadOnlyList<User> assignees, Milestone milestone, int comments, PullRequest pullRequest, DateTimeOffset? closedAt, DateTimeOffset createdAt, DateTimeOffset? updatedAt, int id, string nodeId, bool locked, Repository repository, ReactionSummary reactions, LockReason? activeLockReason) 
                Issue issue = new Issue(
                    "url",
                    "htmlUrl",
                    "commentsUrl",
                    "eventsUrl",
                    iCounter,
                    itemState,
                    $"title {iCounter}",
                    $"body {iCounter}",
                    null,
                    user,
                    null,
                    null,
                    null,
                    null,
                    0,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    null,
                    iCounter+100,
                    "",
                    false,
                    repository,
                    null,
                    null,
                    null
                    );
                issues.Add(issue);
            }
            IReadOnlyList<Issue> readOnlyIssues = issues;
            SearchIssuesResult searchIssuesResult = new SearchIssuesResult(numResults, false, readOnlyIssues);
            SearchIssuesResultReturn = searchIssuesResult;
        }

        /// <summary>
        /// Mock function to get all the reviews for a given pull request.
        /// </summary>
        /// <param name="repositoryId">The Id of the repository</param>
        /// <param name="pullRequestNumber">The pull request number</param>
        /// <returns>IReadOnlyList of PullRequestReview</returns>
        public override Task<IReadOnlyList<PullRequestReview>> GetReviewsForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // The return value needs to be an IReadOnlyList and calling PullRequestReviews.AsReadOnly returns
            // a ReadOnlyCollection, not an IReadOnlyList
            IReadOnlyList<PullRequestReview> readOnlyList = PullRequestReviews;
            return Task.FromResult(readOnlyList);
        }

        /// <summary>
        /// Create fake pull request reviews to return from GetReviewsForPullRequest. The pieces of the 
        /// PullRequestReview that matter for processing are:
        /// 1. PullRequestReview.State == PullRequestReviewState.Approved, only approved pull requests are dismissed,
        /// all other states are ignored.
        /// 2. PullRequestReview.User.Login, for the dismiss message
        /// 3. PullRequestReview.Id which is the Id of the review, itself, and is necessary for the dismissal.
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="pullRequestNumber"></param>
        public void CreateFakeReviewsForPullRequest(int numApproved, int numNotApproved)
        {
            // Since we're using FakeUser1..N in the json payloads, start the FakeUsers for reviews at 100
            int fakeUserNumber = 100;

            for (int iCounter = 0;iCounter<numApproved;iCounter++)
            {
                fakeUserNumber++;
                User fakeUser = CreateFakeUser($"FakeUser{fakeUserNumber}");
                PullRequestReviews.Add(CreateFakeReview(fakeUser, PullRequestReviewState.Approved, fakeUserNumber));
            }

            for (int iCounter = 0; iCounter < numNotApproved; iCounter++)
            {
                fakeUserNumber++;
                User fakeUser = CreateFakeUser($"FakeUser{fakeUserNumber}");
                // The state for not approved reviews doesn't matter, only approved does.
                PullRequestReviews.Add(CreateFakeReview(fakeUser, PullRequestReviewState.ChangesRequested, fakeUserNumber));
            }
        }

        /// <summary>
        /// Convenience to create fake users. Unfortunately, this is necessary becaue Octokit's classes have public getters
        /// and private setters. This means everything needs to be passed into the constructor.
        /// </summary>
        /// <param name="fakeLogin">This will become string that'll be the User.Login</param>
        /// <returns>Octokit.User</returns>
        internal User CreateFakeUser(string fakeLogin)
        {
            DateTimeOffset dateTimeOffset= DateTimeOffset.Now;
            User fakeUser = new User(
            // string avatarUrl,
            "https://avatars.githubusercontent.com/u/13556087?v=4",
            // string bio,
            null,
            // string blog,
            null,
            // int collaborators,
            0,
            // string company,
            null,
            // DateTimeOffset createdAt,
            dateTimeOffset,
            // DateTimeOffset updatedAt,
            dateTimeOffset,
            // int diskUsage,
            0,
            // string email,
            $"{fakeLogin}@fake.com",
            // int followers,
            0,
            // int following,
            0,
            // bool? hireable,
            null,
            // string htmlUrl,
            $"https://github.com/{fakeLogin}",
            // int totalPrivateRepos,
            0,
            // int id,
            12345,
            // string location,
            null,
            // string login,
            fakeLogin,
            // string name,
            fakeLogin,
            // string nodeId,
            null,
            // int ownedPrivateRepos,
            0,
            // Plan plan,
            null,
            // int privateGists,
            0,
            // int publicGists,
            0,
            // int publicRepos,
            0,
            // string url,
            null,
            // RepositoryPermissions permissions,
            null,
            // bool siteAdmin,
            false,
            // string ldapDistinguishedName,
            null,
            // DateTimeOffset? suspendedAt
            null
                );

            return fakeUser;
        }

        /// <summary>
        /// Convenience to create fake PullRequestReviews. Unfortunately, this is necessary becaue Octokit's classes have public 
        /// getters and private setters. This means everything needs to be passed into the constructor.
        /// </summary>
        /// <param name="fakeUser">fake Octokit.User, created using CreateFakeUser</param>
        /// <param name="prReviewState">Octokit.PullRequestReviewState, the state of the review</param>
        /// <param name="prReviewId">Fake prReviewId, it'll match the number of the fakeUser{number}</param>
        /// <returns>Octkit.PullRequestReview</returns>
        internal PullRequestReview CreateFakeReview(User fakeUser, PullRequestReviewState prReviewState, long prReviewId)
        {
            PullRequestReview prReview = new PullRequestReview(
                // long id,
                prReviewId,
                // string nodeId,
                null,
                // string commitId,
                null,
                // User user,
                fakeUser,
                // string body,
                null,
                // string htmlUrl,
                null,
                // string pullRequestUrl,
                null,
                // PullRequestReviewState state,
                prReviewState,
                // AuthorAssociation authorAssociation,
                AuthorAssociation.Collaborator,
                // DateTimeOffset submittedAt
                DateTimeOffset.Now
                );
            return prReview;
        }

        public override Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // The return value needs to be an IReadOnlyList and calling PullRequestFiles.AsReadOnly returns
            // a ReadOnlyCollection, not an IReadOnlyList
            IReadOnlyList<PullRequestFile> readOnlyList = PullRequestFiles;
            return Task.FromResult(readOnlyList);
        }

        public void CreateFakePullRequestFiles(List<string> prFiles)
        {
            foreach (string file in prFiles)
            {
                // The only thing we actually care about here is the fileName (includes path)
                // PullRequestFile(string sha, string fileName, string status, int additions, int deletions, int changes, string blobUrl, string rawUrl, string contentsUrl, string patch, string previousFileName)
                PullRequestFile prFile = new PullRequestFile(
                    //string sha,
                    "",
                    //string fileName,
                    file,
                    //string status,
                    "",
                    //int additions,
                    0,
                    //int deletions,
                    0,
                    //int changes,
                    0,
                    //string blobUrl,
                    "",
                    //string rawUrl,
                    "",
                    //string contentsUrl,
                    "",
                    //string patch,
                    "",
                    //string previousFileName
                    ""
                    );
                PullRequestFiles.Add(prFile);
            }
        }

        // The GitHubClient, for testing purposes, should not be set. It's not going to be authenticating
        // or calling GitHub as part of the mock.
        public override GitHubClient? CreateClientWithGitHubEnvToken(string productHeaderName)
        {
            return null;
        }

        /// <summary>
        /// For testing purposes, if the rulesConfigLocation is null then create a rulesConfiguration with
        /// all rules defaulting to RuleState.On, otherwise load the rulesConfiguration from the location.
        /// </summary>
        /// <param name="rulesConfigLocation">Rules configuration location</param>
        /// <returns>RulesConfiguration</returns>
        public override RulesConfiguration LoadRulesConfiguration(string? rulesConfigLocation = null)
        {
            RulesConfiguration? rulesConfiguration = null;
            if (rulesConfigLocation != null)
            {
                rulesConfiguration = new RulesConfiguration(rulesConfigLocation);
            }
            else
            {
                rulesConfiguration = new RulesConfiguration();
                rulesConfiguration.CreateDefaultConfig(RuleState.On);
            }
            return rulesConfiguration;
        }

        // The mock won't be calling the actual service and since the method it's overriding is
        // async, the warning needs to be disabled.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<List<string>> QueryAILabelService(IssueEventGitHubPayload issueEventPayload)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return AILabelServiceReturn;
        }

        /// <summary>
        /// Convenience function for testing to get the list of comment updates 
        /// </summary>
        /// <returns>List of GitHubComment<GitHubComment></returns>
        public List<GitHubComment> GetComments()
        {
            return _gitHubComments;
        }

        /// <summary>
        /// Convenience function for testing to get the list of dismissals 
        /// </summary>
        /// <returns>List of GitHubReviewDismissal</returns>
        public List<GitHubReviewDismissal> GetReviewDismissals()
        {
            return _gitHubReviewDismissals;
        }

        /// <summary>
        /// Convenience function for testing, get the issue update stored on the GitHubEventClient, rather
        /// than creating one from the issue or pull request payload
        /// </summary>
        /// <returns>Octokit.net IssueUpdate</returns>
        public IssueUpdate GetIssueUpdate()
        {
            return _issueUpdate;
        }

        /// <summary>
        /// Convenience function for testing, get labels to add stored on the GitHubEventClient
        /// </summary>
        /// <returns>List of strings</returns>
        public List<string> GetLabelsToAdd()
        {
            return _labelsToAdd;
        }

        /// <summary>
        /// Convenience function for testing, get labels to remove stored on the GitHubEventClient
        /// </summary>
        /// <returns>List of strings</returns>
        public List<string> GetLabelsToRemove()
        {
            return _labelsToRemove;
        }

        /// <summary>
        /// Convenience function for testing, get the list of GitHub issues to update. For normal action
        /// processing this list won't be used as actions make changes to a common IssueUpdate. For scheduled,
        /// or cron, tasks, those will potentially end up updating multiple, differnt issues.
        /// </summary>
        /// <returns>List of GitHubIssueToUpdate</returns>
        public List<GitHubIssueToUpdate> GetGitHubIssuesToUpdate()
        {
            return _gitHubIssuesToUpdate;
        }

        /// <summary>
        /// Convenience function for testing, get the list of GitHub issues to update. For normal action
        /// processing this list won't be used as actions make changes to a common IssueUpdate. For scheduled,
        /// or cron, tasks, those will potentially end up updating multiple, differnt issues.
        /// </summary>
        /// <returns>List of GitHubIssueToUpdate</returns>
        public List<GitHubIssueToLock> GetGitHubIssuesToLock()
        {
            return _gitHubIssuesToLock;
        }

        public GitHubIssueAssignment GetGitHubIssueAssignment()
        {
            return _gitHubIssueAssignment;
        }

        /// <summary>
        /// Override for the GitHubEventClient funcion which computes this based upon the repository's core rate limit.
        /// Since this isn't necessary for the tests, just return the 100 which is the size of one page.
        /// </summary>
        /// <returns>int</returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<int> ComputeScheduledTaskUpdateLimit()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return 100;
        }
    }
}
