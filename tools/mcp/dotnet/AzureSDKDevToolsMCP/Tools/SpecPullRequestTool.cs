// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Text.Json;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using AzureSDKDSpecTools.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Octokit;

namespace AzureSDKDevToolsMCP.Tools
{
    [Description("TypeSpec pull request tools")]
    [McpServerToolType]
    public class SpecPullRequestTool(IGitHubService _service, IGitHelper _helper, ILogger<SpecPullRequestTool> _logger)
    {
        private readonly IGitHubService gitHubService = _service;
        private readonly IGitHelper gitHelper = _helper;
        private readonly ILogger<SpecPullRequestTool> logger = _logger;
        readonly string TEST_IGNORE_TAG = "[TEST-IGNORE]";


        [McpServerTool, Description("Connect to GitHub using personal access token.")]
        public async Task<string> GetGitHubUserDetails()
        {
            var user = await this.gitHubService.GetGitUserDetails();
            return user != null
                ? $"Connected to GitHub as {user.Login}"
                : "Failed to connect to GitHub. Please make sure to login to GitHub using gh auth login to connect to GitHub.";
        }

        [McpServerTool, Description("Get Pull Request Status: Get TypeSpec pull request status for a given pull request number.")]
        public async Task<string> GetPullRequestStatus(int pullRequestNumber, string typeSpecProjectPath, string repoOwner = "Azure")
        {
            try
            {
                logger.LogInformation($"Getting pull request details for {pullRequestNumber}...");
                logger.LogInformation($"Repo owner: {repoOwner}");
                var repoPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
                logger.LogInformation($"Repo path: {repoPath}");
                var repoName = gitHelper.GetRepoName(repoPath);
                logger.LogInformation($"Repo name: {repoName}");
                var pullRequest = await gitHubService.GetPullRequestAsync(repoOwner, repoName, pullRequestNumber);
                var prStatus = pullRequest.State == ItemState.Open ? "Open" : "Closed";
                var mergeStatus = pullRequest.Merged ? "Merged" : "Not Merged";
                if (pullRequest.State == ItemState.Open)
                {
                    mergeStatus = pullRequest.Mergeable == true ? "Open (PR is Mergeable)" : "Open (PR is not ready to merge)";
                }
                return JsonSerializer.Serialize(pullRequest);
            }
            catch (Exception ex)
            {
                return $"Failed to get pull request details: {ex.Message}";
            }
        }

        [McpServerTool, Description("Get TypeSpec pull request checks for a given pull request number and TypeSpec project path. This tool calls Get Pull Request tool.")]
        public async Task<List<String>> GetPullRequestChecks(int pullRequestNumber, string typeSpecProjectPath, string repoOwner = "Azure")
        {
            var repoPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
            var repoName = gitHelper.GetRepoName(repoPath);
            var checkResults = new List<string>();
            var pullRequest = await GetPullRequestStatus(pullRequestNumber, repoPath);
            if (pullRequest != null)
            {
                var checks = await gitHubService.GetPullRequestChecksAsync(repoOwner, repoName, pullRequestNumber);
                foreach (var check in checks)
                {
                    checkResults.Add($"Name: {check.Name}, Ignore failure: {check.Name.Contains(TEST_IGNORE_TAG)}, Status: {check.Status}, Output: {check.Output}, Conclusion: {check.Conclusion}, Link: {check.HtmlUrl}");
                }
            }
            if (checkResults.Count == 0)
            {
                checkResults.Add("No checks found for the pull request.");
            }
            else
            {
                checkResults.Add($"Total checks found: {checkResults.Count}");
                checkResults.Add($"Total checks ignored: {checkResults.Count(check => check.Contains(TEST_IGNORE_TAG))}. Any failures for ignorable check can be ignored.");
            }
            return checkResults;
        }

        [McpServerTool, Description("Check if TypeSpec project is in public repo.")]
        public bool CheckIfSpecInPublicRepo(string typeSpecProjectPath)
        {
            var repoRootPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
            var isPublicRepo = gitHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
            return isPublicRepo;
        }

        [McpServerTool, Description("Get pull request for current branch")]
        public async Task<string> GetPullRequestForCurrentBranch(string typeSpecProjectPath, string repoOwner = "Azure")
        {
            var repoRootPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
            if (string.IsNullOrEmpty(repoRootPath))
            {
                return "Failed to get repo root path. Please make sure to select the TypeSpec project path.";
            }
            var repoName = gitHelper.GetRepoName(repoRootPath);
            var headRepoOwner = await gitHelper.GetRepoOwnerName(repoRootPath, false);
            var headBranchName = gitHelper.GetBranchName(repoRootPath);
            var headBranchRef = $"{headRepoOwner}:{headBranchName}";
            logger.LogInformation($"Repo name: {repoName}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranchRef}");
            if (string.IsNullOrEmpty(headBranchName) || string.IsNullOrEmpty(repoName) || string.IsNullOrEmpty(headRepoOwner))
            {
                return "Failed to get repo details. Please make sure to select the TypeSpec project path and try again.";
            }

            logger.LogInformation($"Getting pull request for branch {headBranchRef}...");
            var pullRequest = await gitHubService.GetPullRequestForBranchAsync(repoOwner, repoName, headBranchRef);
            return pullRequest != null
                ? $"Pull request found: {pullRequest.HtmlUrl}"
                : "No pull request found for the current branch.";
        }

        [McpServerTool, Description("Create pull request for spec changes. Creates a pull request for committed changes in the current branch.")]
        public async Task<List<string>> CreatePullRequest(string title, string description, string typeSpecProjectPath, string targetBranch = "main", string targetRepoOwner = "Azure")
        {
            List<string> results = new();
            //Get head branch name
            var repoRootPath = gitHelper.GetRepoRootPath(typeSpecProjectPath);
            var headBranchName = gitHelper.GetBranchName(repoRootPath);
            if (string.IsNullOrEmpty(headBranchName) || headBranchName.Equals("main"))
            {
                results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                return results;
            }

            //Get repo details like target owner, head owner, repo name
            var repoName = gitHelper.GetRepoName(repoRootPath);
            var headRepoOwner = await gitHelper.GetRepoOwnerName(repoRootPath, false);

            var headBranch = $"{headRepoOwner}:{headBranchName}";
            logger.LogInformation($"Repo name: {repoName}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}");
            logger.LogInformation($"Creating pull request in {targetRepoOwner}:{repoName}");
            //Create pull request
            var createResponseList = await gitHubService.CreatePullRequest(repoName, targetRepoOwner, targetBranch, headBranch, title, description);
            results.AddRange(createResponseList);
            return results;
        }

        [McpServerTool, Description("Get pull request comments.")]
        public async Task<List<string>> GetPullRequestComments(int pullRequestNumber, string repoName, string repoOwner = "Azure")
        {
            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return new List<string>() { "No comments found for the pull request." };
            }
            return comments;
        }
    }
}
