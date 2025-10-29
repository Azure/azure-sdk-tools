// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Azure.Sdk.Tools.Cli.Tools.GitHub
{
    [Description("Pull request tools")]
    [McpServerToolType]
    public class PullRequestTools(
        IGitHubService gitHubService,
        IGitHelper gitHelper,
        ISpecPullRequestHelper prHelper,
        ILogger<PullRequestTools> logger
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-pr", "Pull request tools")];

        // Commands
        private const string getPullRequestForCurrentBranchCommandName = "get-pr-for-current-branch";
        private const string createPullRequestCommandName = "create-pr";
        private const string getPullRequestCommandName = "get-pr-details";

        // Options
        private readonly Option<string> repoPathOpt = new("--repo-path")
        {
            Description = "Path to repository root",
            Required = true,
        };

        private readonly Option<string> titleOpt = new("--title")
        {
            Description = "Title for the pull request",
            Required = true,
        };

        private readonly Option<string> descriptionOpt = new("--description")
        {
            Description = "Description for the pull request",
            Required = true,
        };

        private readonly Option<bool> draftOpt = new("--draft")
        {
            Description = "Create pull request as draft (default: true)",
            Required = false,
            DefaultValueFactory = _ => true,
        };

        private readonly Option<string> targetBranchOpt = new("--target-branch")
        {
            Description = "Target branch for the pull request",
            Required = false,
            DefaultValueFactory = _ => "main",
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Pull request number",
            Required = true,
        };

        protected override List<Command> GetCommands() =>
        [
            new(getPullRequestForCurrentBranchCommandName, "Get pull request for current branch") { repoPathOpt },
            new(createPullRequestCommandName, "Create pull request")
            {
                titleOpt, descriptionOpt, repoPathOpt, targetBranchOpt, draftOpt,
            },
            new(getPullRequestCommandName, "Get pull request details") { pullRequestNumberOpt, repoPathOpt }
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var commandName = parseResult.CommandResult.Command.Name;
            switch (commandName)
            {
                case getPullRequestForCurrentBranchCommandName:
                    var repoPath = parseResult.GetValue(repoPathOpt);
                    var pullRequestLink = await GetPullRequestForCurrentBranch(repoPath);
                    return new DefaultCommandResponse { Result = "Pull request link: " + pullRequestLink };
                case createPullRequestCommandName:
                    var title = parseResult.GetValue(titleOpt);
                    var description = parseResult.GetValue(descriptionOpt);
                    var createPrRepoPath = parseResult.GetValue(repoPathOpt);
                    var targetBranch = parseResult.GetValue(targetBranchOpt);
                    var draft = parseResult.GetValue(draftOpt);
                    var createPullRequestResponse = await CreatePullRequest(title, description, createPrRepoPath, targetBranch, draft);
                    return new DefaultCommandResponse { Result = "Create pull request response: " + string.Join("\n", createPullRequestResponse) };
                case getPullRequestCommandName:
                    var pullRequestNumber = parseResult.GetValue(pullRequestNumberOpt);
                    var getPRrepoPath = parseResult.GetValue(repoPathOpt);
                    var pullRequestDetails = await GetPullRequest(pullRequestNumber, getPRrepoPath);
                    return new DefaultCommandResponse { Result = "Pull request details: " + pullRequestDetails };
                default:
                    return new DefaultCommandResponse { ResponseError = "Unknown command: " + commandName };
            }
        }


        [McpServerTool(Name = "azsdk_get_github_user_details"), Description("Connect to GitHub using personal access token.")]
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

        [McpServerTool(Name = "azsdk_get_pull_request_link_for_current_branch"), Description("Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details.")]
        public async Task<DefaultCommandResponse> GetPullRequestForCurrentBranch(string repoPath)
        {
            try
            {
                var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
                logger.LogInformation("GitHub repo root path: {RepoRootPath}", repoRootPath);
                if (string.IsNullOrEmpty(repoRootPath))
                {
                    return new DefaultCommandResponse { ResponseError = "Failed to get repo root path. Please make sure to provide a valid repository path." };
                }
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath);
                var repoName = gitHelper.GetRepoName(repoRootPath);
                var headBranchName = gitHelper.GetBranchName(repoRootPath);
                var headBranchRef = $"{repoOwner}:{headBranchName}";
                var forkOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
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
                response += await GetPullRequest(pullRequest.Number, repoPath);
                return new DefaultCommandResponse { Result = response };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to find pull request for current branch");
                return new DefaultCommandResponse { ResponseError = $"Failed to find pull request for current branch, Error: {ex.Message}" };
            }
        }

        [McpServerTool(Name = "azsdk_create_pull_request"), Description("Create pull request for repository changes. Provide title, description and path to repository root. Creates a pull request for committed changes in the current branch.")]
        public async Task<DefaultCommandResponse> CreatePullRequest(string title, string description, string repoPath, string targetBranch = "main", bool draft = true)
        {
            try
            {
                List<string> results = [];
                try
                {
                    // Discover the repository root from the provided path
                    var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
                    var headBranchName = gitHelper.GetBranchName(repoRootPath);
                    if (string.IsNullOrEmpty(headBranchName) || headBranchName.Equals("main"))
                    {
                        results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                    }

                    // Get repo details like target owner, head owner, repo name
                    var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
                    var targetRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, true);
                    var repoName = gitHelper.GetRepoName(repoRootPath);

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

        private async Task<List<string>> GetPullRequestCommentsAsync(int pullRequestNumber, string repoPath)
        {
            var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
            var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath);
            var repoName = gitHelper.GetRepoName(repoRootPath);

            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return ["No comments found for the pull request."];
            }
            return comments;
        }


        [McpServerTool(Name = "azsdk_get_pull_request"), Description("This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews.")]
        public async Task<DefaultCommandResponse> GetPullRequest(int pullRequestNumber, string repoPath)
        {
            try
            {
                var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath);
                var repoName = gitHelper.GetRepoName(repoRootPath);

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
                    Comments = await GetPullRequestCommentsAsync(pullRequestNumber, repoPath)
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
