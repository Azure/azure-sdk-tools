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

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("Pull request tools")]
    [McpServerToolType]
    public class SpecPullRequestTools(
        IGitHubService gitHubService,
        IGitHelper gitHelper,
        ISpecPullRequestHelper prHelper,
        ILogger<SpecPullRequestTools> logger,
        IOutputService output,
        ITypeSpecHelper typeSpecHelper) : MCPTool
    {
        private readonly static string REPO_OWNER = "Azure";
        private readonly static string REPO_NAME = "azure-rest-api-specs";

        // Commands
        private const string checkIfSpecInPublicRepoCommandName = "check-if-repo-is-public";
        private const string getPullRequestForCurrentBranchCommandName = "get-pr-for-current-branch";
        private const string createPullRequestCommandName = "create-pr";
        private const string getPullRequestCommandName = "get-pr-details";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };
        private readonly Option<string> titleOpt = new(["--title"], "Title for the pull request") { IsRequired = true };
        private readonly Option<string> descriptionOpt = new(["--description"], "Description for the pull request") { IsRequired = true };
        private readonly Option<string> targetBranchOpt = new(["--target-branch"], () => "main", "Target branch for the pull request") { IsRequired = false };
        private readonly Option<int> pullRequestNumberOpt = new(["--pr"], "Pull request number") { IsRequired = true };
        private readonly Option<string> repoOwnerOpt = new(["--repo-owner"], () => "Azure", "GitHub repo owner") { IsRequired = false };
        private readonly Option<string> repoNameOpt = new(["--repo-name"], () => "azure-rest-api-specs", "GitHub repo name") { IsRequired = false };


        [McpServerTool, Description("Connect to GitHub using personal access token.")]
        public async Task<string> GetGitHubUserDetailsAsync()
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

        [McpServerTool, Description("Check if TypeSpec project is in public repo. Provide absolute path to TypeSpec project root as param.")]
        public string CheckIfSpecInPublicRepo(string typeSpecProjectPath)
        {
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                var isPublicRepo = typeSpecHelper.IsRepoPathForPublicSpecRepo(repoRootPath);
                return output.Format(isPublicRepo);
            }
            catch (Exception ex)
            {
                SetFailure();
                return output.Format($"Unexpected failure occurred. Error: {ex.Message}");
            }
        }

        [McpServerTool, Description("Get pull request link for current branch in the repo. Provide absolute path to TypeSpec project root as param. This tool call GetPullRequest to get pull request details.")]
        public async Task<string> GetPullRequestForCurrentBranchAsync(string typeSpecProjectPath)
        {
            try
            {
                var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                logger.LogInformation($"GitHub repo root path: {repoRootPath}");
                if (string.IsNullOrEmpty(repoRootPath))
                {
                    return output.Format("Failed to get repo root path. Please make sure to select the TypeSpec project path.");
                }
                var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);
                var headBranchName = gitHelper.GetBranchName(repoRootPath);
                var headBranchRef = $"{headRepoOwner}:{headBranchName}";
                logger.LogInformation($"Repo name: {REPO_NAME}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranchRef}");
                if (string.IsNullOrEmpty(headRepoOwner))
                {
                    return output.Format("Failed to get repo details. Please make sure to select the TypeSpec project path and try again.");
                }

                logger.LogInformation("Getting pull request for branch {headBranchRef}...", headBranchRef);
                var pullRequest = await gitHubService.GetPullRequestForBranchAsync(REPO_OWNER, REPO_NAME, headBranchRef);
                if (pullRequest == null)
                {
                    return output.Format("No pull request found for the current branch.");
                }

                string response = $"Pull request found: {pullRequest.HtmlUrl}";
                response += await GetPullRequestAsync(pullRequest.Number);
                return output.Format(response);
            }
            catch (Exception ex)
            {
                return output.Format($"Failed to find pull request for current branch, Error: {ex.Message}");
            }
        }

        [McpServerTool, Description("Create pull request for spec changes. Provide title, description and absolute path to TypeSpec project root as params. Creates a pull request for committed changes in the current branch.")]
        public async Task<List<string>> CreatePullRequestAsync(string title, string description, string typeSpecProjectPath, string targetBranch = "main")
        {
            try
            {
                List<string> results = [];
                try
                {
                    var repoRootPath = typeSpecHelper.GetSpecRepoRootPath(typeSpecProjectPath);
                    var headBranchName = gitHelper.GetBranchName(repoRootPath);
                    if (string.IsNullOrEmpty(headBranchName) || headBranchName.Equals("main"))
                    {
                        SetFailure();
                        results.Add("Failed to create pull request. Pull request can not be created for changes in main branch. Select the GitHub branch for your spec changes using `git checkout <branch name>'");
                        return results;
                    }

                    //Get repo details like target owner, head owner, repo name
                    var headRepoOwner = await gitHelper.GetRepoOwnerNameAsync(repoRootPath, false);

                    var headBranch = $"{headRepoOwner}:{headBranchName}";
                    logger.LogInformation($"Repo name: {REPO_NAME}, Head repo owner: {headRepoOwner}, Head branch name: {headBranchName}, Head branch ref: {headBranch}");
                    logger.LogInformation($"Creating pull request in {REPO_OWNER}:{REPO_NAME}");
                    //Create pull request
                    var createResponseList = await gitHubService.CreatePullRequestAsync(REPO_NAME, REPO_OWNER, targetBranch, headBranch, title, description);
                    results.AddRange(createResponseList);
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

        private async Task<List<string>> GetPullRequestCommentsAsync(int pullRequestNumber, string repoName, string repoOwner)
        {
            var comments = await gitHubService.GetPullRequestCommentsAsync(repoOwner, repoName, pullRequestNumber);
            if (comments == null || comments.Count == 0)
            {
                return ["No comments found for the pull request."];
            }
            return comments;
        }


        [McpServerTool, Description("This tool gets pull request details, status, comments, checks, next action details, links to APIView reviews.")]
        public async Task<string> GetPullRequestAsync(int pullRequestNumber, string repoOwner = "Azure", string repoName = "azure-rest-api-specs")
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
                    Comments = await GetPullRequestCommentsAsync(pullRequestNumber, repoName, repoOwner)
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
            var command = new Command("spec-pr");
            var subCommands = new[] {
                new Command(checkIfSpecInPublicRepoCommandName, "Check if API spec is in public repo") { typeSpecProjectPathOpt },
                new Command(getPullRequestForCurrentBranchCommandName, "Get pull request for current branch") { typeSpecProjectPathOpt },
                new Command(createPullRequestCommandName, "Create pull request") { titleOpt, descriptionOpt, typeSpecProjectPathOpt, targetBranchOpt },
                new Command(getPullRequestCommandName, "Get pull request details") { pullRequestNumberOpt, repoOwnerOpt, repoNameOpt }
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
                case checkIfSpecInPublicRepoCommandName:
                    var isPublic = CheckIfSpecInPublicRepo(commandParser.GetValueForOption(typeSpecProjectPathOpt));
                    logger.LogInformation("Is spec in public repo: {isPublic}", isPublic);
                    return 0;
                case getPullRequestForCurrentBranchCommandName:
                    var pullRequestLink = await GetPullRequestForCurrentBranchAsync(commandParser.GetValueForOption(typeSpecProjectPathOpt));
                    logger.LogInformation("Pull request link: {pullRequestLink}", pullRequestLink);
                    return 0;
                case createPullRequestCommandName:
                    var title = commandParser.GetValueForOption(titleOpt);
                    var description = commandParser.GetValueForOption(descriptionOpt);
                    var typeSpecProject = commandParser.GetValueForOption(typeSpecProjectPathOpt);
                    var targetBranch = commandParser.GetValueForOption(targetBranchOpt);
                    var createPullRequestResponse = await CreatePullRequestAsync(title, description, typeSpecProject, targetBranch);
                    logger.LogInformation("Create pull request response: {createPullRequestResponse}", createPullRequestResponse);
                    return 0;
                case getPullRequestCommandName:
                    var pullRequestNumber = commandParser.GetValueForOption(pullRequestNumberOpt);
                    var repoOwner = commandParser.GetValueForOption(repoOwnerOpt);
                    var repoName = commandParser.GetValueForOption(repoNameOpt);
                    var pullRequestDetails = await GetPullRequestAsync(pullRequestNumber, repoOwner, repoName);
                    logger.LogInformation("Pull request details: {pullRequestDetails}", pullRequestDetails);
                    return 0;
                default:
                    logger.LogError("Unknown command: {commandName}", commandName);
                    return 1;
            }
        }
    }
}
