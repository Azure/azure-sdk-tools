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
        /// IsUserMemberOfOrg value. Defaults to false.
        /// </summary>
        public bool IsUserMemberOfOrgReturn { get; set; } = false;

        public List<PullRequestReview> PullRequestReviews { get; set; } = new List<PullRequestReview>();
        public List<PullRequestFile> PullRequestFiles { get; set; } = new List<PullRequestFile>();

        public MockGitHubEventClient(string productHeaderName, string? rulesConfigLocation = null) : 
            base(productHeaderName, rulesConfigLocation)
        {

        }

        /// <summary>
        /// The mock ProcessPendingUpdates should just return the number of updates
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="issueOrPullRequestNumber"></param>
        /// <returns></returns>
        public override Task<int> ProcessPendingUpdates(long repositoryId, int issueOrPullRequestNumber)
        {
            int numUpdates = 0;
            if (this._issueUpdate != null)
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, Issue Update is non-null");
                numUpdates++;
            }
            else
            {
                Console.WriteLine("MockGitHubEventClient::ProcessPendingUpdates, Issue Update is null");
            }
            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of pending comments = {this._gitHubComments.Count}");
            numUpdates += this._gitHubComments.Count;
            Console.WriteLine($"MockGitHubEventClient::ProcessPendingUpdates, number of pending dismissals = {this._gitHubReviewDismissals.Count}");
            numUpdates += this._gitHubReviewDismissals.Count;
            return Task.FromResult(numUpdates);
        }

        /// <summary>
        /// IsUserCollaborator override. Returns IsCollaboratorReturn value
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public override Task<bool> IsUserCollaborator(long repositoryId, string user)
        {
            return Task.FromResult(IsCollaboratorReturn);
        }

        /// <summary>
        /// IsUserMemberOfOrg override. Returns IsUserMemberOfOrgReturn value
        /// </summary>
        /// <param name="orgName"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public override Task<bool> IsUserMemberOfOrg(string orgName, string user)
        {
            return Task.FromResult(IsCollaboratorReturn);
        }


        /// <summary>
        /// DoesUserHavePermissions override. Returns UserHasPermissionsReturn value
        /// </summary>
        /// <param name="repositoryId"></param>
        /// <param name="user"></param>
        /// <param name="permissionList"></param>
        /// <returns></returns>
        public override Task<bool> DoesUserHavePermissions(long repositoryId, string user, List<PermissionLevel> permissionList)
        {
            return Task.FromResult(UserHasPermissionsReturn);
        }

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
        /// <returns></returns>
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
        /// <returns></returns>
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
        /// <returns></returns>
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

        /// <summary>
        /// Convenience function for testing to get the list of comment updates 
        /// </summary>
        /// <returns>List<GitHubComment></returns>
        public List<GitHubComment> GetComments()
        {
            return _gitHubComments;
        }

        /// <summary>
        /// Convenience function for testing to get the list of dismissals 
        /// </summary>
        /// <returns></returns>
        public List<GitHubReviewDismissal> GetReviewDismissals()
        {
            return _gitHubReviewDismissals;
        }

        /// <summary>
        /// Convenience function for testing, get the issue update stored on the GitHubEventClient, rather
        /// than creating one from the issue or pull request payload
        /// </summary>
        /// <returns></returns>
        public IssueUpdate GetIssueUpdate()
        {
            return _issueUpdate;
        }
    }
}
