// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;

namespace Azure.Sdk.Tools.Cli.Tools.GitHub
{
    [Description("Pull request tools")]
    [McpServerToolType]
    public class PullRequestTools(
        IGitHubService gitHubService,
        IGitHelper gitHelper,
        ISpecPullRequestHelper prHelper,
        ILogger<PullRequestTools> logger,
        IOutputHelper output) : MCPTool
    {
        // Commands
        private const string getPullRequestForCurrentBranchCommandName = "get-pr-for-current-branch";
        private const string createPullRequestCommandName = "create-pr";
        private const string getPullRequestCommandName = "get-pr-details";

        // Options
        private readonly Option<string> repoPathOpt = new(["--repo-path"], "Path to repository root") { IsRequired = true };
        private readonly Option<string> titleOpt = new(["--title"], "Title for the pull request") { IsRequired = true };
        private readonly Option<string> descriptionOpt = new(["--description"], "Description for the pull request") { IsRequired = true };
        private readonly Option<bool> draftOpt = new(["--draft"], () => true, "Create pull request as draft (default: true)");
        private readonly Option<string> targetBranchOpt = new(["--target-branch"], () => "main", "Target branch for the pull request") { IsRequired = false };
        private readonly Option<int> pullRequestNumberOpt = new(["--pr"], "Pull request number") { IsRequired = true };


        [McpServerTool(Name = "azsdk_get_github_user_details"), Description("Connect to GitHub using personal access token.")]
        public async Task<string> GetGitHubUserDetails()
        {
            try
            {
                var user = await gitHubService.GetGitUserDetailsAsync();
                return user != null
                    ? output.Format($"Connected to GitHub as {user.Login}")
                    : output.Format("Failed to connect to GitHub. Please make sure to login to GitHub using gh auth login to connect to GitHub.");
            }
            catch(Exception ex)
            {
                SetFailure();
                return output.Format($"Failed to connect to GitHub. Unhandled error: {ex.Message}");
            }

        }

        [McpServerTool(Name = "azsdk_get_pull_request_link_for_current_branch"), Description("Get pull request link for current branch in the repo. Provide absolute path to repository root as param. This tool call GetPullRequest to get pull request details.")]
        public async Task<string> GetPullRequestForCurrentBranch(string repoPath)
        {
            try
            {
                var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
                logger.LogInformation($"GitHub repo root path: {repoRootPath}");
                if (string.IsNullOrEmpty(repoRootPath))
                {
                    return output.Format("Failed to get repo root path. Please make sure to provide a valid repository path.");
                }
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
                var repoName = gitHelper.GetRepoName(repoRootPath);
                var headBranchName = gitHelper.GetBranchName(repoRootPath);
                var headBranchRef = $"{repoOwner}:{headBranchName}";
                logger.LogInformation($"Repo name: {repoName}, Repo owner: {repoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranchRef}");
                if (string.IsNullOrEmpty(repoOwner))
                {
                    return output.Format("Failed to get repo details. Please make sure to provide a valid repository path and try again.");
                }

                logger.LogInformation("Getting pull request for branch {headBranchRef}...", headBranchRef);
                var pullRequest = await gitHubService.GetPullRequestForBranchAsync(repoOwner, repoName, headBranchRef);
                if (pullRequest == null)
                {
                    return output.Format("No pull request found for the current branch.");
                }

                string response = $"Pull request found: {pullRequest.HtmlUrl}";
                response += await GetPullRequest(pullRequest.Number, repoPath);
                return output.Format(response);
            }
            catch (Exception ex)
            {
                return output.Format($"Failed to find pull request for current branch, Error: {ex.Message}");
            }
        }

        [McpServerTool(Name = "azsdk_create_pull_request"), Description("Create pull request for repository changes. Provide title, description and path within repository. Creates a pull request for committed changes in the current branch.")]
        public async Task<List<string>> CreatePullRequest(string title, string description, string repoPath, string targetBranch = "main", bool draft = true)
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
                        SetFailure();
                        results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                        return results;
                    }

                    //Get repo details like target owner, head owner, repo name
                    var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
                    var targetRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, true);
                    var repoName = gitHelper.GetRepoName(repoRootPath);

                    var headBranch = $"{headRepoOwner}:{headBranchName}";
                    logger.LogInformation($"Repo name: {repoName}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}");
                    logger.LogInformation("Repo name: {repoName}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}", repoName, headRepoOwner, headBranchName, headBranch);
                    logger.LogInformation("Creating pull request in {targetRepoOwner}:{repoName}", targetRepoOwner, repoName);
                    //Create pull request
                    var createResponse = await gitHubService.CreatePullRequestAsync(repoName, targetRepoOwner, targetBranch, headBranch, title, description, draft);
                    results.AddRange(createResponse.Messages);
                    return results;
                }
                catch (Exception ex)
                {
                    SetFailure();
                    results.Add($"Failed to create a pull request, Error: {ex.Message}");
                    return results;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected exception occurred: {ex.Message}");
                SetFailure();
                return new List<string> { $"Unhandled exception: {ex.Message}" };
            }
        }

        private async Task<List<string>> GetPullRequestCommentsAsync(int pullRequestNumber, string repoPath)
        {
            var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
            var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
            var repoName = gitHelper.GetRepoName(repoRootPath);
            
            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return ["No comments found for the pull request."];
            }
            return comments;
        }


        [McpServerTool(Name = "azsdk_get_pull_request"), Description("This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews.")]
        public async Task<string> GetPullRequest(int pullRequestNumber, string repoPath)
        {
            try
            {
                var repoRootPath = gitHelper.DiscoverRepoRoot(repoPath);
                var repoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
                var repoName = gitHelper.GetRepoName(repoRootPath);
                
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
                    Comments = await GetPullRequestCommentsAsync(pullRequestNumber, repoPath)
                };

                // Get PR check statuses
                logger.LogInformation("Getting pull request checks");
                prDetails.Checks.AddRange(await gitHubService.GetPullRequestChecksAsync(pullRequestNumber, repoName, repoOwner));

                // Parse APi reviews and add the information
                logger.LogInformation("Searching for API review links in comments");
                var apiviewlinks = prHelper.FindApiReviewLinks(prDetails.Comments);
                if (apiviewlinks != null && apiviewlinks.Count > 0)
                {
                    prDetails.ApiViews.AddRange(apiviewlinks);
                }

                return JsonSerializer.Serialize(prDetails);
            }
            catch (Exception ex)
            {
                logger.LogError("{exception}", ex.Message);
                return output.Format($"Failed to get pull request summary. Error {ex.Message}");
            }
        }

        public override Command GetCommand()
        {
            var command = new Command("spec-pr", "Pull request tools");
            var subCommands = new[] {
                new Command(getPullRequestForCurrentBranchCommandName, "Get pull request for current branch") { repoPathOpt },
                new Command(createPullRequestCommandName, "Create pull request") { titleOpt, descriptionOpt, repoPathOpt, targetBranchOpt, draftOpt },
                new Command(getPullRequestCommandName, "Get pull request details") { pullRequestNumberOpt, repoPathOpt }
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var commandName = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;
            switch (commandName)
            {
                case getPullRequestForCurrentBranchCommandName:
                    var repoPath = commandParser.GetValueForOption(repoPathOpt);
                    var pullRequestLink = await GetPullRequestForCurrentBranch(repoPath);
                    logger.LogInformation("Pull request link: {pullRequestLink}", pullRequestLink);
                    return 0;
                case createPullRequestCommandName:
                    var title = commandParser.GetValueForOption(titleOpt);
                    var description = commandParser.GetValueForOption(descriptionOpt);
                    var createPrRepoPath = commandParser.GetValueForOption(repoPathOpt);
                    var targetBranch = commandParser.GetValueForOption(targetBranchOpt);
                    var draft = commandParser.GetValueForOption(draftOpt);
                    var createPullRequestResponse = await CreatePullRequest(title, description, createPrRepoPath, targetBranch, draft);
                    logger.LogInformation("Create pull request response: {createPullRequestResponse}", createPullRequestResponse);
                    return 0;
                case getPullRequestCommandName:
                    var pullRequestNumber = commandParser.GetValueForOption(pullRequestNumberOpt);
                    var getPRrepoPath = commandParser.GetValueForOption(repoPathOpt);
                    var pullRequestDetails = await GetPullRequest(pullRequestNumber, getPRrepoPath);
                    logger.LogInformation("Pull request details: {pullRequestDetails}", pullRequestDetails);
                    return 0;
                default:
                    logger.LogError("Unknown command: {commandName}", commandName);
                    return 1;
            }
        }
    }
}