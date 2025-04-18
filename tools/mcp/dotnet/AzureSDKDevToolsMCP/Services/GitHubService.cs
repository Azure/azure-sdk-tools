// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Octokit;
using Octokit.Models;
using Octokit.Clients;
using System.Runtime.InteropServices;

namespace AzureSDKDevToolsMCP.Services
{
    public interface  IGitHubService
    {
        public Task<User> GetGitUserDetails();
        public Task<PullRequest> GetPullRequestForCommitAsync(string repoOwner, string repoName, string commitSha);
        public Task<IReadOnlyList<CheckRun>> GetPullRequestChecksAsync(string repoOwner, string repoName, int pullRequestNumber);
        public Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber);
    }

    public class GitHubService : IGitHubService
    {
        private readonly GitHubClient _gitHubClient;
        private bool IsConnected => _gitHubClient != null && _gitHubClient.Credentials != null;
        public GitHubService()
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN");
            _gitHubClient = new GitHubClient(new ProductHeaderValue("AzureSDKDevToolsMCP"))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };
        }

        public async Task<User> GetGitUserDetails()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("GitHub client is not connected. Please check and ensure that GitHub PAT is valid.");
            }
            var user = await _gitHubClient.User.Current();
            return user;
        }

        public async Task<IReadOnlyList<CheckRun>> GetPullRequestChecksAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            var pr = await GetPullRequestAsync(repoOwner, repoName, pullRequestNumber) ?? throw new InvalidOperationException($"Pull request {pullRequestNumber} not found.");
            var checks = await _gitHubClient.Check.Run.GetAllForReference(repoOwner, repoName, pr.Head.Sha);
            return checks == null
                ? throw new InvalidOperationException($"Check runs for pull request {pullRequestNumber} not found.")
                : checks.CheckRuns;
        }

        public async Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("GitHub client is not connected.");
            }
            var pullRequest = await _gitHubClient.PullRequest.Get(repoOwner, repoName, pullRequestNumber);
            return pullRequest;
        }

        public async Task<PullRequest> GetPullRequestForCommitAsync(string repoOwner, string repoName, string commitSha)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("GitHub client is not connected.");
            }

            var pullRequests = await _gitHubClient.PullRequest.GetAllForRepository(repoOwner, repoName);
            if (pullRequests == null || pullRequests.Count == 0)
            {
                throw new InvalidOperationException($"No pull requests found for repository {repoOwner}/{repoName}.");
            }
            var pr = pullRequests.FirstOrDefault(pr => pr.Head.Sha.Equals(commitSha, StringComparison.OrdinalIgnoreCase) || pr.Base.Sha.Equals(commitSha, StringComparison.OrdinalIgnoreCase));
            if (pr == null)
            {
                throw new InvalidOperationException($"No pull request found for commit {commitSha} in repository {repoOwner}/{repoName}.");
            }
            return pr;
        }
    }
}
