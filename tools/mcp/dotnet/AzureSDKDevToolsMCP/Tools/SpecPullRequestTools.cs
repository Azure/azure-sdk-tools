// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDevToolsMCP.Services;
using AzureSDKDSpecTools.Helpers;
using AzureSDKDSpecTools.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureSDKDevToolsMCP.Tools
{
    [Description("Pull request tools")]
    [McpServerToolType]
    public class SpecPullRequestTools(IGitHubService gitHubService, 
        IGitHelper gitHelper, 
        ISpecPullRequestHelper prHelper,
        ILogger<SpecPullRequestTools> logger,
        ITypeSpecHelper typeSpecHelper)
    {
        private readonly IGitHubService gitHubService = gitHubService;
        private readonly IGitHelper gitHelper = gitHelper;
        private readonly ILogger<SpecPullRequestTools> logger = logger;
        private readonly ISpecPullRequestHelper prHelper = prHelper;
        private readonly ITypeSpecHelper typeSpecHelper = typeSpecHelper;
        private readonly static string REPO_OWNER = "Azure";
        private readonly static string REPO_NAME = "azure-rest-api-specs";

        [McpServerTool, Description("Connect to GitHub using personal access token.")]
        public async Task<string> GetGitHubUserDetails()
        {
            var user = await this.gitHubService.GetGitUserDetails();
            return user != null
                ? $"Connected to GitHub as {user.Login}"
                : "Failed to connect to GitHub. Please make sure to login to GitHub using gh auth login to connect to GitHub.";
        }

        [McpServerTool, Description("Check if TypeSpec project is in public repo. Provide absolute path to TypeSpec project root as param.")]
        public bool CheckIfSpecInPublicRepo(string typeSpecProjectPath)
        {
            var repoRootPath = this.typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
            var isPublicRepo = this.typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
            return isPublicRepo;
        }

        [McpServerTool, Description("Get pull request link for current branch in the repo. Provide absolute path to TypeSpec project root as param. This tool call GetPullRequest to get pull request details.")]
        public async Task<string> GetPullRequestForCurrentBranch(string typeSpecProjectPath)
        {
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                logger.LogInformation($"GitHub repo root path: {repoRootPath}");
                if (string.IsNullOrEmpty(repoRootPath))
                {
                    return "Failed to get repo root path. Please make sure to select the TypeSpec project path.";
                }
                var headRepoOwner = await gitHelper.GetRepoOwnerName(repoRootPath, false);
                var headBranchName = gitHelper.GetBranchName(repoRootPath);
                var headBranchRef = $"{headRepoOwner}:{headBranchName}";
                logger.LogInformation($"Repo name: {REPO_NAME}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranchRef}");
                if (string.IsNullOrEmpty(headRepoOwner))
                {
                    return "Failed to get repo details. Please make sure to select the TypeSpec project path and try again.";
                }

                logger.LogInformation($"Getting pull request for branch {headBranchRef}...");
                var pullRequest = await gitHubService.GetPullRequestForBranchAsync(REPO_OWNER, REPO_NAME, headBranchRef);
                if (pullRequest == null)
                {
                    return "No pull request found for the current branch.";
                }

                string response = $"Pull request found: {pullRequest.HtmlUrl}";
                response += await GetPullRequest(pullRequest.Number);
                return response;
            }
            catch (Exception ex)
            {
                return $"Failed to find pull request for current branch, Error: {ex.Message}";
            }            
        }

        [McpServerTool, Description("Create pull request for spec changes. Provide title, description and absolute path to TypeSpec project root as params. Creates a pull request for committed changes in the current branch.")]
        public async Task<List<string>> CreatePullRequest(string title, string description, string typeSpecProjectPath, string targetBranch = "main")
        {
            List<string> results = new();
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                var headBranchName = gitHelper.GetBranchName(repoRootPath);
                if (string.IsNullOrEmpty(headBranchName) || headBranchName.Equals("main"))
                {
                    results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                    return results;
                }

                //Get repo details like target owner, head owner, repo name
                var headRepoOwner = await gitHelper.GetRepoOwnerName(repoRootPath, false);

                var headBranch = $"{headRepoOwner}:{headBranchName}";
                logger.LogInformation($"Repo name: {REPO_NAME}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}");
                logger.LogInformation($"Creating pull request in {REPO_OWNER}:{REPO_NAME}");
                //Create pull request
                var createResponseList = await gitHubService.CreatePullRequest(REPO_NAME, REPO_OWNER, targetBranch, headBranch, title, description);
                results.AddRange(createResponseList);
                return results;
            }
            catch(Exception ex)
            {
                results.Add($"Failed to create a pull request, Error: {ex.Message}");
                return results;
            }            
        }

        private async Task<List<string>> GetPullRequestComments(int pullRequestNumber, string repoName, string repoOwner)
        {
            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return ["No comments found for the pull request."];
            }
            return comments;
        }


        [McpServerTool, Description("This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews.")]
        public async Task<string> GetPullRequest(int pullRequestNumber, string repoOwner = "Azure", string repoName = "azure-rest-api-specs")
        {
            try
            {
                logger.LogInformation($"Getting pull request details for {pullRequestNumber} in repo {repoName}");
                var pullRequest = await gitHubService.GetPullRequestAsync(repoOwner, repoName, pullRequestNumber);
                PullRequestDetails prDetails = new()
                {
                    // Get PR basics and comments
                    pullRequestNumber = pullRequest.Number,
                    Url = pullRequest.HtmlUrl,
                    Status = pullRequest.State.StringValue,
                    IsMerged = pullRequest.Merged,
                    IsMergeable = pullRequest.Mergeable ?? false,
                    Author = pullRequest.User.Name,
                    AssignedTo = pullRequest.Assignee?.Name ?? "",
                    Labels = pullRequest.Labels?.ToList() ?? [],
                    Comments = await GetPullRequestComments(pullRequestNumber, repoName, repoOwner)
                };

                // Get PR check statuses
                logger.LogInformation("Getting pull request checks");
                prDetails.Checks.AddRange(await gitHubService.GetPullRequestChecks(pullRequestNumber, repoName, repoOwner));

                // Parse APi reviews and add the information
                logger.LogInformation("Searching for API review links in comments");
                var apiviewlinks = prHelper.FindApiReviewLinks(prDetails.Comments);
                if (apiviewlinks != null &&  apiviewlinks.Count > 0)
                {
                    prDetails.ApiViews.AddRange(apiviewlinks);
                }
                      
                 return JsonSerializer.Serialize(prDetails);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return $"Failed to get pull request summary. Error {ex.Message}";
            }            
        }
    }
}
