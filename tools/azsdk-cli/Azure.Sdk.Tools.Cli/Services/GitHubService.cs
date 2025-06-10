// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Extensions.Logging;
using Octokit;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services
{
    public interface IGitHubService
    {
        public Task<User> GetGitUserDetailsAsync();
        public Task<List<String>> GetPullRequestChecksAsync(int pullRequestNumber, string repoName, string repoOwner);
        public Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber);
        public Task<string> GetGitHubParentRepoUrlAsync(string owner, string repoName);
        public Task<List<string>> CreatePullRequestAsync(string repoName, string repoOwner, string baseBranch, string headBranch, string title, string body);
        public Task<List<string>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullRequestNumber);
        public Task<PullRequest?> GetPullRequestForBranchAsync(string repoOwner, string repoName, string remoteBranch);
        public Task<Issue> GetIssueAsync(string repoOwner, string repoName, int issueNumber);
    }

    public class GitHubService : IGitHubService
    {
        private GitHubClient gitHubClient;
        private ILogger<GitHubService> logger;

        private const string CreatedByCopilotLabel = "Created by copilot";

        public GitHubService(ILogger<GitHubService> _logger)
        {
            logger = _logger;
            var token = GetGitHubAuthToken();
            gitHubClient = new GitHubClient(new ProductHeaderValue("AzureSDKDevToolsMCP"))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };
        }

        public async Task<User> GetGitUserDetailsAsync()
        {
            var user = await gitHubClient.User.Current();
            return user;
        }

        public async Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            var pullRequest = await gitHubClient.PullRequest.Get(repoOwner, repoName, pullRequestNumber);
            return pullRequest;
        }

        private static string GetGitHubAuthToken()
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string command = isWindows ? "cmd.exe" : "gh";
            string args = isWindows ? "/C gh auth token" : "auth token";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to get GitHub auth token. Error: {process.StandardError.ReadToEnd()}");
                }
                return output.Trim();
            }
        }

        public async Task<string> GetGitHubParentRepoUrlAsync(string owner, string repoName)
        {
            var repository = await gitHubClient.Repository.Get(owner, repoName);
            if (repository == null)
            {
                throw new InvalidOperationException($"Repository {owner}/{repoName} not found in GitHub.");
            }
            return repository.Parent?.Url ?? repository.Url;
        }

        public async Task<PullRequest?> GetPullRequestForBranchAsync(string repoOwner, string repoName, string remoteBranch)
        {
            logger.LogInformation($"Getting all pull request for {repoOwner}/{repoName}");
            var pullRequests = await gitHubClient.PullRequest.GetAllForRepository(repoOwner, repoName);
            logger.LogInformation($"Branch name: {remoteBranch}");
            return pullRequests?.FirstOrDefault(pr => pr.Head?.Label != null && pr.Head.Label.Equals(remoteBranch, StringComparison.InvariantCultureIgnoreCase));
        }

        private async Task<bool> IsDiffMergeableAsync(string targetRepoOwner, string repoName, string baseBranch, string headBranch)
        {
            logger.LogInformation("Comparing the head branch against target branch");
            var comparison = await gitHubClient.Repository.Commit.Compare(targetRepoOwner, repoName, baseBranch, headBranch);
            logger.LogInformation($"Comparison: {comparison.Status}");
            return comparison?.MergeBaseCommit != null;
        }

        public async Task<List<string>> CreatePullRequestAsync(string repoName, string repoOwner, string baseBranch, string headBranch, string title, string body)
        {
            var responseList = new List<string>();
            // Check if a pull request already exists for the branch
            try
            {
                var pr = await GetPullRequestForBranchAsync(repoOwner, repoName, headBranch);
                if (pr != null)
                {
                    responseList.Add($"Pull request already exists for branch {headBranch} in repository {repoOwner}/{repoName}. Pull request URL: {pr.HtmlUrl}");
                    return responseList;
                }
                responseList.Add($"No pull request found for branch {headBranch} in repository {repoOwner}/{repoName}. Proceeding to create a new pull request.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                responseList.Add($"Failed to check for existing pull request for the branch. Error: {ex.Message}");
                return responseList;
            }

            // Check mergeability of the branches
            try
            {
                responseList.Add($"Checking if changes are mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]...");
                var isMergeable = await IsDiffMergeableAsync(repoOwner, repoName, baseBranch, headBranch);
                if (!isMergeable)
                {
                    responseList.Add($"Changes from [{repoOwner}] are not mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]. Please resolve the conflicts and try again.");
                    responseList.Add($"By default, target branch in main. If you are trying to create a pull request to a different branch, please specify the target branch and try again.");
                    return responseList;
                }
            }
            catch (Exception ex)
            {
                responseList.Add($"Failed to check if changes are mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]. Error: {ex.Message}");
                return responseList;
            }

            // Create the pull request
            PullRequest? createdPullRequest = null;
            try
            {
                responseList.Add($"Changes are mergeable. Proceeding to create pull request for changes in {headBranch}.");
                var pullRequest = new NewPullRequest(title, headBranch, baseBranch)
                {
                    Body = body
                };

                createdPullRequest = await gitHubClient.PullRequest.Create(repoOwner, repoName, pullRequest);
                if (createdPullRequest == null)
                {
                    responseList.Add($"Failed to create pull request for changes in {headBranch}.");
                    return responseList;
                }
                responseList.Add($"Pull request created successfully. Pull request URL: {createdPullRequest.HtmlUrl}");
            }
            catch (Exception ex)
            {
                responseList.Add($"Failed to create pull request. Error: {ex.Message}");
                return responseList;
            }

            try
            {
                // Add label
                await gitHubClient.Issue.Labels.AddToIssue(repoOwner, repoName, createdPullRequest.Number, [CreatedByCopilotLabel]);
            }
            catch (Exception ex)
            {
                responseList.Add($"Failed to add label '{CreatedByCopilotLabel}' to the pull request. Error: {ex.Message}");
            }
            return responseList;
        }

        public async Task<List<string>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullRequestNumber)
        {
            List<string> responseList = [];
            try
            {
                var comments = await gitHubClient.Issue.Comment.GetAllForIssue(repoOwner, repoName, pullRequestNumber);
                if (comments == null || comments.Count == 0)
                {
                    responseList.Add($"No comments found for pull request {pullRequestNumber}.");
                    return responseList;
                }
                foreach (var comment in comments)
                {
                    responseList.Add($"Comment by {comment.User.Login}: {comment.Body}");
                }
                return responseList;
            }
            catch (Exception ex)
            {
                responseList.Add($"Failed to get comments for pull request {pullRequestNumber}. Error: {ex.Message}");
                return responseList;
            }
        }

        public async Task<List<String>> GetPullRequestChecksAsync(int pullRequestNumber, string repoName, string repoOwner)
        {
            var checkResults = new List<string>();
            try
            {
                var pr = await GetPullRequestAsync(repoOwner, repoName, pullRequestNumber);
                if (pr == null)
                {
                    logger.LogError($"Pull request {pullRequestNumber} not found");
                    throw new NotFoundException($"Pull request {pullRequestNumber} not found.", System.Net.HttpStatusCode.NotFound);
                }

                var checkResponse = await gitHubClient.Check.Run.GetAllForReference(repoOwner, repoName, pr.Head.Sha);
                if (checkResponse == null || checkResponse.TotalCount == 0)
                {
                    logger.LogError("No checkruns found for pull request.");
                    return ["No checks found for the pull request."];
                }

                var checkRuns = checkResponse.CheckRuns.Where(c => !c.Name.StartsWith("[TEST-IGNORE]"));
                foreach (var check in checkRuns)
                {
                    checkResults.Add($"Name: {check.Name}, Status: {check.Status}, Output: {check.Output.Summary}, Conclusion: {check.Conclusion}, Link: {check.HtmlUrl}");
                }
                checkResults.Add($"Total checks found: {checkResults.Count}");
                int pendingRequiedChecks = checkRuns.Count(check => check.Status != CheckStatus.Completed || check.Conclusion == CheckConclusion.Failure);
                checkResults.Add($"Failed checks: {checkRuns.Count(check => check.Conclusion == CheckConclusion.Failure)}");
                checkResults.Add($"Pending required checks to merge the PR: {pendingRequiedChecks}.");
            }
            catch (Exception ex)
            {
                checkResults.Add($"Failed to get Github pull request checks, Error: {ex.Message}");
            }
            return checkResults;
        }

        /// <summary>
        /// Gets the issue details for a given issue number in a specified repository.
        /// </summary>
        /// <param name="repoOwner"></param>
        /// <param name="repoName"></param>
        /// <param name="issueNumber"></param>
        /// <returns></returns>
        public async Task<Issue> GetIssueAsync(string repoOwner, string repoName, int issueNumber)
        {
            return await gitHubClient.Issue.Get(repoOwner, repoName, issueNumber);
        }
    }
}
