// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using ModelContextProtocol.Server;
using Octokit;

namespace AzureSDKDevToolsMCP.Tools
{
    [Description("TypeSpec pull request tools")]
    [McpServerToolType]
    public class SpecPullRequestTool(IGitHubService service, IGitHelper helper)
    {
        private readonly IGitHubService gitHubService = service;
        private readonly IGitHelper gitHelper = helper;
        private bool isGitHubUserVerified = false;
        private string gitHubUserName = string.Empty;

        [McpServerTool, Description("Connect to GitHub using personal access token.")]
        public async Task<string> ConnectToGitHub()
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN");
            if(string.IsNullOrEmpty(token))
            {
                return "GitHub personal access token is not set. Please set the GITHUB_PERSONAL_ACCESS_TOKEN environment variable.";
            }
            if (!isGitHubUserVerified)
            {
                var user = await this.gitHubService.GetGitUserDetails();
                if (user != null)
                {
                    gitHubUserName = user.Login;
                    isGitHubUserVerified = true;
                    return $"Connected to GitHub as {gitHubUserName}";
                }
                else
                {
                    return "Failed to connect to GitHub.";
                }
            }
            return $"Connected to GitHub as {gitHubUserName}";
        }

        [McpServerTool, Description("Get Pull Request: Get TypeSpec pull request details for a given pull request number. This tool calls Connect to GitHub tool to verify GitHub connection.")]
        public async Task<string> GetPullRequestDetails(int pullRequestNumber, string repoPath)
        {
            await ConnectToGitHub();
            var repoOwner = gitHelper.GetRepoOwnerName(repoPath);
            var repoName = gitHelper.GetRepoName(repoPath);
            var pullRequest = await gitHubService.GetPullRequestAsync(repoOwner, repoName, pullRequestNumber);
            return "{" +
                   $"Pull request url: {pullRequest.HtmlUrl}, " +
                   $"Title: {pullRequest.Title}, " +
                   $"State: {pullRequest.State}, " +
                   $"Created at: {pullRequest.CreatedAt}, " +
                   $"Updated at: {pullRequest.UpdatedAt}" +
                   "}";
        }

        [McpServerTool, Description("Get TypeSpec pull request comments for a given pull request number. This tool calls Get Pull Request tool.")]
        public async Task<List<String>> GetPullRequestChecks(int pullRequestNumber, string repoPath)
        {
            var repoOwner = gitHelper.GetRepoOwnerName(repoPath);
            var repoName = gitHelper.GetRepoName(repoPath);

            var checkResults = new List<string>();
            var pullRequest = await GetPullRequestDetails(pullRequestNumber, repoPath);
            if (pullRequest != null)
            {
                var checks = await gitHubService.GetPullRequestChecksAsync(repoOwner, repoName, pullRequestNumber);
                foreach(var check in checks)
                {
                    checkResults.Add($"Name: {check.Name}, Status: {check.Status}, Output: {check.Output}, Conclusion: {check.Conclusion}, Link: {check.HtmlUrl}");
                }
            }
            if (checkResults.Count == 0)
            {
                checkResults.Add("No checks found for the pull request.");
            }
            else
            {
                checkResults.Add($"Total checks found: {checkResults.Count}");
            }
            return checkResults;
        }

        [McpServerTool, Description("Get TypeSpec pull request for current commit sha")]
        public async Task<string> GetSpecPullRequest(string typeSpecProjectPath)
        {
            var repoRootPath = this.gitHelper.GetRepoRootPath(typeSpecProjectPath);
            var currentCommitSha = gitHelper.GetCurrentCommitSha(repoRootPath);
            var currentBranchName = gitHelper.GetCurrentBranchName(repoRootPath);
            var repoOwner = gitHelper.GetRepoOwnerName(repoRootPath);
            var repoName = gitHelper.GetRepoName(repoRootPath);

            var pullRequest = await gitHubService.GetPullRequestForCommitAsync(repoOwner, repoName, currentCommitSha);
            if (pullRequest != null)
            {
                return $"Pull request found: {pullRequest.HtmlUrl}";
            }
            else
            {
                return $"No pull request found for commit {currentCommitSha} on branch {currentBranchName}.";
            }
        }

        [McpServerTool, Description("Check if TypeSpec project is in public repo.")]
        public bool CheckIfSpecInPublicRepo(string typeSpecProjectPath)
        {
            var repoRootPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
            var isPublicRepo = gitHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
            return isPublicRepo;
        }
    }
}
