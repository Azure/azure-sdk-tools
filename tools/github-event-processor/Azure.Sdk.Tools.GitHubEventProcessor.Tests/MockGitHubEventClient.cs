using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
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

        public override Task<IReadOnlyList<PullRequestFile>> GetFilesForPullRequest(long repositoryId, int pullRequestNumber)
        {
            // The return value needs to be an IReadOnlyList and calling PullRequestFiles.AsReadOnly returns
            // a ReadOnlyCollection, not an IReadOnlyList
            IReadOnlyList<PullRequestFile> readOnlyList = PullRequestFiles;
            return Task.FromResult(readOnlyList);
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
