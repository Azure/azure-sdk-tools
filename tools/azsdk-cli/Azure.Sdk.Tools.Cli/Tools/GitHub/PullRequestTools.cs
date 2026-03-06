// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.GitHub
{
    [Description("Pull request tools")]
    [McpServerToolType]
    public class PullRequestTools(
        IGitHubService gitHubService,
        IGitHelper gitHelper,
        ISpecPullRequestHelper prHelper,
        ILogger<PullRequestTools> logger
    ) : MCPNoCommandTool
    {
        // MCP Tool Names
        private const string GetGitHubUserDetailsToolName = "azsdk_get_github_user_details";
        private const string GetPullRequestLinkToolName = "azsdk_get_pull_request_link_for_current_branch";
        private const string CreatePullRequestToolName = "azsdk_create_pull_request";
        private const string GetPullRequestToolName = "azsdk_get_pull_request";

        [McpServerTool(Name = GetGitHubUserDetailsToolName), Description("Get GitHub user details and profile information. Find out who a GitHub user is by their username.")]
        public async Task<DefaultCommandResponse> GetGitHubUserDetails()
        {
            try
            {
                var user = await gitHubService.GetGitUserDetailsAsync();
                return user != null
                    ? new DefaultCommandResponse { Result = $"Connected to GitHub as {user.Login}" }
                    : new DefaultCommandResponse { ResponseError = "Failed to connect to GitHub. Please make sure to login to GitHub using gh auth login to connect to GitHub." };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to GitHub");
                return new DefaultCommandResponse { ResponseError = $"Failed to connect to GitHub. Unhandled error: {ex.Message}" };
            }

        }

        [McpServerTool(Name = GetPullRequestLinkToolName), Description("Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details.")]
        public async Task<DefaultCommandResponse> GetPullRequestForCurrentBranch(string repoPath, CancellationToken ct)
        {
            try
            {
                var repoRootPath = await gitHelper.DiscoverRepoRootAsync(repoPath, ct);
                logger.LogInformation("GitHub repo root path: {RepoRootPath}", repoRootPath);
                if (string.IsNullOrEmpty(repoRootPath))
                {
                    return new DefaultCommandResponse { ResponseError = "Failed to get repo root path. Please make sure to provide a valid repository path." };
                }
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, ct: ct);
                var repoName = await gitHelper.GetRepoNameAsync(repoRootPath, ct);
                var headBranchName = await gitHelper.GetBranchNameAsync(repoRootPath, ct);
                var forkOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false, ct);
                var headBranchRef = $"{forkOwner}:{headBranchName}";
                logger.LogInformation(
                    "Repo name: {RepoName}, Repo owner: {RepoOwner}, Head branch name: {HeadBranchName}, Head branch ref: {HeadBranchRef}",
                    repoName,
                    repoOwner,
                    headBranchName,
                    headBranchRef);
                if (string.IsNullOrEmpty(repoOwner))
                {
                    return new DefaultCommandResponse { ResponseError = "Failed to get repo details. Please make sure to provide a valid repository path and try again." };
                }

                logger.LogInformation("Getting pull request for branch {HeadBranchRef}...", headBranchRef);
                var pullRequest = await gitHubService.GetPullRequestForBranchAsync(repoOwner, repoName, headBranchRef);
                if (pullRequest == null)
                {
                    return new DefaultCommandResponse { ResponseError = "No pull request found for the current branch." };
                }

                string response = $"Pull request found: {pullRequest.HtmlUrl}";
                response += await GetPullRequest(pullRequest.Number, repoPath, ct);
                return new DefaultCommandResponse { Result = response };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to find pull request for current branch");
                return new DefaultCommandResponse { ResponseError = $"Failed to find pull request for current branch, Error: {ex.Message}" };
            }
        }

        [McpServerTool(Name = CreatePullRequestToolName), Description("Create pull request for repository changes. Provide title, description and path to repository root. Creates a pull request for committed changes in the current branch.")]
        public async Task<DefaultCommandResponse> CreatePullRequest(string title, string description, string repoPath, string targetBranch = "main", bool draft = true, CancellationToken ct = default)
        {
            try
            {
                List<string> results = [];
                try
                {
                    // Discover the repository root from the provided path
                    var repoRootPath = await gitHelper.DiscoverRepoRootAsync(repoPath, ct);
                    var headBranchName = await gitHelper.GetBranchNameAsync(repoRootPath, ct);
                    if (string.IsNullOrEmpty(headBranchName) || headBranchName.Equals("main"))
                    {
                        results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                    }

                    // Get repo details like target owner, head owner, repo name
                    var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false, ct);
                    var targetRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, true, ct);
                    var repoName = await gitHelper.GetRepoNameAsync(repoRootPath, ct);

                    var headBranch = $"{headRepoOwner}:{headBranchName}";
                    logger.LogInformation("Repo name: {repoName}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}",
                                            repoName, headRepoOwner, headBranchName, headBranch);
                    logger.LogInformation("Creating pull request in {targetRepoOwner}:{repoName}", targetRepoOwner, repoName);
                    // Create pull request
                    var createResponse = await gitHubService.CreatePullRequestAsync(repoName, targetRepoOwner, targetBranch, headBranch, title, description, draft);
                    results.AddRange(createResponse.Messages);
                    return new DefaultCommandResponse { Result = string.Join(Environment.NewLine, results) };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create a pull request");
                    return new DefaultCommandResponse
                    {
                        Message = string.Join(Environment.NewLine, results),
                        ResponseError = $"Failed to create a pull request, Error: {ex.Message}"
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred while creating pull request");
                return new DefaultCommandResponse { ResponseError = $"An unexpected error occurred: {ex.Message}" };
            }
        }

        private async Task<List<string>> GetPullRequestCommentsAsync(int pullRequestNumber, string repoPath, CancellationToken ct)
        {
            var repoRootPath = await gitHelper.DiscoverRepoRootAsync(repoPath, ct);
            var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath);
            var repoName = await gitHelper.GetRepoNameAsync(repoRootPath, ct);

            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return ["No comments found for the pull request."];
            }
            return comments;
        }


        [McpServerTool(Name = GetPullRequestToolName), Description("This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews.")]
        public async Task<DefaultCommandResponse> GetPullRequest(int pullRequestNumber, string repoPath, CancellationToken ct)
        {
            try
            {
                var repoRootPath = await gitHelper.DiscoverRepoRootAsync(repoPath, ct);
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, ct: ct);
                var repoName = await gitHelper.GetRepoNameAsync(repoRootPath, ct);

                logger.LogInformation("Getting pull request details for {pullRequestNumber} in repo {repoOwner}/{repoName}",
                                        pullRequestNumber, repoOwner, repoName);
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
                    Comments = await GetPullRequestCommentsAsync(pullRequestNumber, repoPath, ct)
                };

                // Get PR check statuses
                logger.LogInformation("Getting pull request checks");
                prDetails.Checks.AddRange(await gitHubService.GetPullRequestChecksAsync(pullRequestNumber, repoName, repoOwner));

                // Parse API reviews and add the information
                logger.LogInformation("Searching for API review links in comments");
                var apiviewlinks = prHelper.FindApiReviewLinks(prDetails.Comments);
                if (apiviewlinks != null && apiviewlinks.Count > 0)
                {
                    prDetails.ApiViews.AddRange(apiviewlinks);
                }

                return new DefaultCommandResponse { Result = JsonSerializer.Serialize(prDetails) };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get pull request");
                return new DefaultCommandResponse { ResponseError = $"Failed to get pull request. Error: {ex.Message}" };
            }
        }
    }
}
