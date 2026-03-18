// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Octokit;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Services
{
    public enum CreateBranchStatus
    {
        Created,
        AlreadyExists,
    }

    public class PullRequestResult
    {
        public string Url { get; set; } = string.Empty;
        public List<string> Messages { get; set; } = new List<string>();
    }

    public class GitConnection
    {
        private GitHubClient? _gitHubClient; // Backing field for the property

        public GitHubClient gitHubClient
        {
            get
            {
                if (_gitHubClient == null)
                {
                    var token = GetGitHubAuthToken();
                    _gitHubClient = new GitHubClient(new ProductHeaderValue("AzureSDKDevToolsMCP"))
                    {
                        Credentials = new Credentials(token, AuthenticationType.Bearer)
                    };
                }
                return _gitHubClient;
            }
        }

        private static string GetGitHubAuthToken()
        {
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
            token = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
            // If the GITHUB_TOKEN environment variable is not set, try to get the token using the 'gh' CLI command
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
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to get GitHub auth token. Error:{Environment.NewLine}{errorOutput}{Environment.NewLine}{Environment.NewLine}Please make sure GitHub CLI is installed and make sure to login using `gh auth login` to connect to GitHub.");
                }
                return output.Trim();
            }
        }
    }

    public interface IGitHubService
    {
        public Task<User> GetGitUserDetailsAsync(CancellationToken ct);
        public Task<List<String>> GetPullRequestChecksAsync(int pullRequestNumber, string repoName, string repoOwner, CancellationToken ct);
        public Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct);
        public Task<string> GetGitHubParentRepoUrlAsync(string owner, string repoName, CancellationToken ct);
        public Task<PullRequestResult> CreatePullRequestAsync(string repoName, string repoOwner, string baseBranch, string headBranch, string title, string body, bool draft = true, CancellationToken ct = default);
        public Task<List<string>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct);
        public Task<PullRequest?> GetPullRequestForBranchAsync(string repoOwner, string repoName, string remoteBranch, CancellationToken ct);
        public Task<IReadOnlyList<PullRequest?>> SearchPullRequestsByTitleAsync(string repoOwner, string repoName, string titleSearchTerm, ItemState? state = ItemState.Open, CancellationToken ct = default);
        public Task<Issue> GetIssueAsync(string repoOwner, string repoName, int issueNumber, CancellationToken ct);
        public Task<Issue> CreateIssueAsync(string repoOwner, string repoName, string title, string body, List<string>? assignees = null, CancellationToken ct = default);
        public Task<string?> GetPullRequestHeadSha(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct);
        public Task<string?> GetFileFromPullRequest(string repoOwner, string repoName, int pullRequestNumber, string filePath, CancellationToken ct);
        public Task<string?> GetFileFromBranch(string repoOwner, string repoName, string branch, string filePath, CancellationToken ct);
        public Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path, CancellationToken ct);
        public Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path, string? branch = null, CancellationToken ct = default);
        public Task UpdatePullRequestAsync(string repoOwner, string repoName, int pullRequestNumber, string title, string body, ItemState state, CancellationToken ct);
        public Task UpdateFileAsync(string owner, string repoName, string path, string message, string content, string sha, string branch, CancellationToken ct);
        public Task<CreateBranchStatus> CreateBranchAsync(string repoOwner, string repoName, string branchName, string baseBranchName = "main", CancellationToken ct = default);
        public Task<bool> IsExistingBranchAsync(string repoOwner, string repoName, string branchName, CancellationToken ct);
        public Task<RepositoryContent> GetContentsSingleAsync(string owner, string repoName, string path, string? branch = null, CancellationToken ct = default);
        public Task<HashSet<string>?> GetPublicOrgMembership(string username, CancellationToken ct);
        public Task<bool> HasWritePermission(string owner, string repo, string username, CancellationToken ct);
        public Task<Octokit.SearchCodeResult> SearchFilesAsync(string searchQuery, CancellationToken ct);
    }

    // We enforce cancellation token usage broadly via an analyzer across this codebase,
    // therefore many methods in this class require a cancellation token that is unused.
    // The Octokit SDK does not support cancellation tokens, but we should still pass them
    // down in case this changes, and also to make all async code appear consistent with conventions.
    public class GitHubService : GitConnection, IGitHubService
    {
        private readonly ILogger<GitHubService> logger;
        private const string CreatedByCopilotLabel = "Created by copilot";

        public GitHubService(ILogger<GitHubService> _logger)
        {
            logger = _logger;
        }

        public async Task<User> GetGitUserDetailsAsync(CancellationToken ct)
        {
            var user = await gitHubClient.User.Current();
            return user;
        }

        public async Task<PullRequest> GetPullRequestAsync(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct)
        {
            var pullRequest = await gitHubClient.PullRequest.Get(repoOwner, repoName, pullRequestNumber);
            return pullRequest;
        }

        public async Task UpdatePullRequestAsync(string repoOwner, string repoName, int pullRequestNumber, string title, string body, ItemState state, CancellationToken ct)
        {
            // This method now accepts title, body, and state directly, so caller must fetch the PR first if needed.
            var update = new PullRequestUpdate
            {
                Title = title,
                Body = body,
                State = state
            };
            await gitHubClient.PullRequest.Update(repoOwner, repoName, pullRequestNumber, update);
        }

        public async Task<Issue> CreateIssueAsync(string repoOwner, string repoName, string title, string body, List<string>? assignees = null, CancellationToken ct = default)
        {
            logger.LogInformation("Creating issue in {RepoOwner}/{RepoName}: {Title}", repoOwner, repoName, title);
            var newIssue = new NewIssue(title)
            {
                Body = body
            };

            if (assignees != null && assignees.Count > 0)
            {
                foreach (var assignee in assignees)
                {
                    newIssue.Assignees.Add(assignee);
                }
            }

            return await gitHubClient.Issue.Create(repoOwner, repoName, newIssue);
        }

        public async Task<string?> GetPullRequestHeadSha(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct)
        {
            try
            {
                logger.LogInformation("Getting head SHA for PR #{PullRequestNumber} in {Owner}/{Repo}", pullRequestNumber, repoOwner, repoName);
                var pr = await gitHubClient.PullRequest.Get(repoOwner, repoName, pullRequestNumber);
                return pr?.Head?.Sha;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get head SHA for PR #{PullRequestNumber}", pullRequestNumber);
                return null;
            }
        }

        public async Task<string?> GetFileFromPullRequest(string repoOwner, string repoName, int pullRequestNumber, string filePath, CancellationToken ct)
        {
            try
            {
                logger.LogInformation("Getting file {FilePath} from PR #{PullRequestNumber} in {Owner}/{Repo}", filePath, pullRequestNumber, repoOwner, repoName);
                var pr = await gitHubClient.PullRequest.Get(repoOwner, repoName, pullRequestNumber);
                if (pr?.Head?.Sha == null)
                {
                    logger.LogWarning("Could not get head SHA for PR #{PullRequestNumber}", pullRequestNumber);
                    return null;
                }

                var contents = await gitHubClient.Repository.Content.GetAllContentsByRef(repoOwner, repoName, filePath, pr.Head.Sha);
                if (contents == null || contents.Count == 0)
                {
                    logger.LogInformation("File {FilePath} not found in PR #{PullRequestNumber}", filePath, pullRequestNumber);
                    return null;
                }

                return contents[0].Content;
            }
            catch (Octokit.NotFoundException)
            {
                logger.LogInformation("File {FilePath} not found in PR #{PullRequestNumber}", filePath, pullRequestNumber);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get file {FilePath} from PR #{PullRequestNumber}", filePath, pullRequestNumber);
                return null;
            }
        }

        public async Task<string?> GetFileFromBranch(string repoOwner, string repoName, string branch, string filePath, CancellationToken ct)
        {
            try
            {
                logger.LogInformation("Getting file {FilePath} from branch {Branch} in {Owner}/{Repo}", filePath, branch, repoOwner, repoName);
                var contents = await gitHubClient.Repository.Content.GetAllContentsByRef(repoOwner, repoName, filePath, branch);
                if (contents == null || contents.Count == 0)
                {
                    logger.LogInformation("File {FilePath} not found in branch {Branch}", filePath, branch);
                    return null;
                }

                return contents[0].Content;
            }
            catch (Octokit.NotFoundException)
            {
                logger.LogInformation("File {FilePath} not found in branch {Branch}", filePath, branch);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get file {FilePath} from branch {Branch}", filePath, branch);
                return null;
            }
        }

        public async Task<string> GetGitHubParentRepoUrlAsync(string owner, string repoName, CancellationToken ct)
        {
            var repository = await gitHubClient.Repository.Get(owner, repoName);
            if (repository == null)
            {
                throw new InvalidOperationException($"Repository {owner}/{repoName} not found in GitHub.");
            }
            return repository.Parent?.Url ?? repository.Url;
        }

        public async Task<PullRequest?> GetPullRequestForBranchAsync(string repoOwner, string repoName, string remoteBranch, CancellationToken ct)
        {
            logger.LogInformation(
                "Searching for pull request in repository {RepoOwner}/{RepoName} for branch {RemoteBranch}",
                repoOwner,
                repoName,
                remoteBranch);
            var pullRequests = await gitHubClient.PullRequest.GetAllForRepository(repoOwner, repoName);
            return pullRequests?.FirstOrDefault(pr => pr.Head?.Label != null && pr.Head.Label.Equals(remoteBranch, StringComparison.InvariantCultureIgnoreCase));
        }

        public async Task<IReadOnlyList<PullRequest?>> SearchPullRequestsByTitleAsync(string repoOwner, string repoName, string titleSearchTerm, ItemState? state = ItemState.Open, CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation(
                    "Searching for pull requests with title containing '{TitleSearchTerm}' in {RepoOwner}/{RepoName}",
                    titleSearchTerm,
                    repoOwner,
                    repoName);

                // Build the search query - remove quotes to enable case-insensitive matching
                var searchQuery = $"repo:{repoOwner}/{repoName} is:pr {titleSearchTerm} in:title";

                // Add state filter
                if (state == ItemState.Open)
                {
                    searchQuery += " is:open";
                }
                else if (state == ItemState.Closed)
                {
                    searchQuery += " is:closed";
                }
                // If neither open nor closed, search all states (don't add filter)

                var searchRequest = new SearchIssuesRequest(searchQuery)
                {
                    Type = IssueTypeQualifier.PullRequest,
                    PerPage = 100 // Maximum allowed by GitHub API
                };

                var searchResult = await gitHubClient.Search.SearchIssues(searchRequest);

                if (searchResult?.Items == null || !searchResult.Items.Any())
                {
                    logger.LogInformation(
                        "No pull requests found with title containing '{TitleSearchTerm}'",
                        titleSearchTerm);
                    return new List<PullRequest>();
                }

                // Convert Issues to PullRequests (GitHub Search API returns Issues for PRs)
                var pullRequests = new List<PullRequest?>();
                foreach (var issue in searchResult.Items)
                {
                    if (issue.PullRequest != null)
                    {
                        // Get the full PR details since search only returns basic info
                        try
                        {
                            var fullPr = await gitHubClient.PullRequest.Get(repoOwner, repoName, issue.Number);
                            pullRequests.Add(fullPr);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(
                                ex,
                                "Failed to get full details for PR #{PullRequestNumber}",
                                issue.Number);
                            // Still add the basic info if we can't get full details
                            pullRequests.Add(null);
                        }
                    }
                }

                logger.LogInformation(
                    "Found {PullRequestCount} pull requests with title containing '{TitleSearchTerm}'.",
                    pullRequests.Count,
                    titleSearchTerm);
                return pullRequests;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching for pull requests with title '{TitleSearchTerm}' in {RepoOwner}/{RepoName}", titleSearchTerm, repoOwner, repoName);
                throw;
            }
        }

        private async Task<bool> IsDiffMergeableAsync(string targetRepoOwner, string repoName, string baseBranch, string headBranch, CancellationToken ct)
        {
            logger.LogInformation("Comparing the head branch against target branch");
            var comparison = await gitHubClient.Repository.Commit.Compare(targetRepoOwner, repoName, baseBranch, headBranch);
            logger.LogInformation("Comparison: {ComparisonStatus}", comparison?.Status);
            return comparison?.MergeBaseCommit != null;
        }

        public async Task<PullRequestResult> CreatePullRequestAsync(string repoName, string repoOwner, string baseBranch, string headBranch, string title, string body, bool draft = true, CancellationToken ct = default)
        {
            var response = new PullRequestResult();
            // Check if a pull request already exists for the branch
            try
            {
                var pr = await GetPullRequestForBranchAsync(repoOwner, repoName, headBranch, ct);
                if (pr != null)
                {
                    response.Messages.Add($"Pull request already exists for branch {headBranch} in repository {repoOwner}/{repoName}");
                    response.Url = pr.HtmlUrl;
                    return response;
                }
                response.Messages.Add($"No pull request found for branch {headBranch} in repository {repoOwner}/{repoName}. Proceeding to create a new pull request.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check for existing pull request for branch {HeadBranch} in repository {RepoOwner}/{RepoName}", headBranch, repoOwner, repoName);
                response.Messages.Add($"Failed to check for existing pull request for the branch. Error: {ex.Message}");
                return response;
            }

            // Check mergeability of the branches
            try
            {
                response.Messages.Add($"Checking if changes are mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]...");
                var isMergeable = await IsDiffMergeableAsync(repoOwner, repoName, baseBranch, headBranch, ct);
                if (!isMergeable)
                {
                    response.Messages.Add($"Changes from [{repoOwner}] are not mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]. Please resolve the conflicts and try again.");
                    response.Messages.Add($"By default, target branch in main. If you are trying to create a pull request to a different branch, please specify the target branch and try again.");
                    return response;
                }
            }
            catch (Exception ex)
            {
                response.Messages.Add($"Failed to check if changes are mergeable to {baseBranch} branch in repository [{repoOwner}/{repoName}]. Error: {ex.Message}");
                return response;
            }

            // Create the pull request
            PullRequest? createdPullRequest = null;
            try
            {
                response.Messages.Add($"Changes are mergeable. Proceeding to create pull request for changes in {headBranch}.");
                var pullRequest = new NewPullRequest(title, headBranch, baseBranch)
                {
                    Body = body,
                    Draft = draft
                };

                createdPullRequest = await gitHubClient.PullRequest.Create(repoOwner, repoName, pullRequest);
                if (createdPullRequest == null)
                {
                    response.Messages.Add($"Failed to create pull request for changes in {headBranch}.");
                    return response;
                }

                if (draft)
                {
                    response.Messages.Add($"Pull request created successfully as draft PR.");
                    response.Url = createdPullRequest.HtmlUrl;
                    response.Messages.Add("Once you have successfully generated the SDK transition the PR to review ready.");
                }

                response.Messages.Add($"Pull request created successfully.");
                response.Url = createdPullRequest.HtmlUrl;
            }
            catch (Exception ex)
            {
                response.Messages.Add($"Failed to create pull request. Error: {ex.Message}");
                return response;
            }

            try
            {
                // Add label
                await gitHubClient.Issue.Labels.AddToIssue(repoOwner, repoName, createdPullRequest.Number, [CreatedByCopilotLabel]);
            }
            catch (Exception ex)
            {
                response.Messages.Add($"Failed to add label '{CreatedByCopilotLabel}' to the pull request. Error: {ex.Message}");
            }
            return response;
        }

        public async Task<List<string>> GetPullRequestCommentsAsync(string repoOwner, string repoName, int pullRequestNumber, CancellationToken ct)
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

        public async Task<List<String>> GetPullRequestChecksAsync(int pullRequestNumber, string repoName, string repoOwner, CancellationToken ct)
        {
            var checkResults = new List<string>();
            try
            {
                var pr = await GetPullRequestAsync(repoOwner, repoName, pullRequestNumber, ct);
                if (pr == null)
                {
                    logger.LogError("Pull request {PullRequestNumber} not found", pullRequestNumber);
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
                int pendingRequiredChecks = checkRuns.Count(check => check.Status != CheckStatus.Completed || check.Conclusion == CheckConclusion.Failure);
                checkResults.Add($"Failed checks: {checkRuns.Count(check => check.Conclusion == CheckConclusion.Failure)}");
                checkResults.Add($"Pending required checks to merge the PR: {pendingRequiredChecks}.");
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
        public async Task<Issue> GetIssueAsync(string repoOwner, string repoName, int issueNumber, CancellationToken ct)
        {
            return await gitHubClient.Issue.Get(repoOwner, repoName, issueNumber);
        }

        /// <summary>
        /// Helper method to get contents from a GitHub repository path.
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="repoName">Repository name</param>
        /// <param name="path">Directory or file path</param>
        /// <param name="expectSingleFile">If true, returns only the first file content; if false, returns all contents</param>
        /// <returns>List of repository contents or null if path doesn't exist</returns>
        public async Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path, CancellationToken ct)
        {
            try
            {
                return await gitHubClient.Repository.Content.GetAllContents(owner, repoName, path);
            }
            catch (NotFoundException)
            {
                logger.LogInformation(
                    "Path {Path} not found in {Owner}/{RepoName}",
                    path,
                    owner,
                    repoName);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching contents from {Owner}/{RepoName}/{Path}", owner, repoName, path);
                throw;
            }
        }

        /// <summary>
        /// Helper method to get contents from a GitHub repository path.
        /// </summary>
        /// <param name="owner">Repository owner</param>
        /// <param name="repoName">Repository name</param>
        /// <param name="path">Directory or file path</param>
        /// <param name="branch">If provided, returns a list of repository contents within the given branch</param>
        /// <returns>List of repository contents or null if path doesn't exist</returns>
        public async Task<IReadOnlyList<RepositoryContent>?> GetContentsAsync(string owner, string repoName, string path, string? branch = null, CancellationToken ct = default)
        {
            try
            {
                IReadOnlyList<RepositoryContent> result;
                if (string.IsNullOrEmpty(branch))
                {
                    result = await gitHubClient.Repository.Content.GetAllContents(owner, repoName, path);
                }
                else
                {
                    result = await gitHubClient.Repository.Content.GetAllContentsByRef(owner, repoName, path, branch);
                }
                return result;
            }
            catch (NotFoundException ex)
            {
                logger.LogInformation("GitHubService: Path {path} not found in {owner}/{repoName} on reference {branch}. Exception: {exception}", path, owner, repoName, branch, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GitHubService: Error fetching contents from {owner}/{repoName}/{path} on reference {branch}", owner, repoName, path, branch);
                throw;
            }
        }

        public async Task UpdateFileAsync(string owner, string repoName, string path, string message, string content, string sha, string branch = null, CancellationToken ct = default)
        {
            try
            {
                if (branch.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Direct updates to main branch are not allowed for safety reasons. Please use a feature branch instead.");
                }

                var updateRequest = new UpdateFileRequest(message, content, sha, branch);
                await gitHubClient.Repository.Content.UpdateFile(owner, repoName, path, updateRequest);
            }
            catch (NotFoundException ex)
            {
                logger.LogInformation("GitHubService: Path {path} not found in {owner}/{repoName} on reference {branch}. Exception: {exception}", path, owner, repoName, branch, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GitHubService: Error fetching contents from {owner}/{repoName}/{path} on reference {branch}", owner, repoName, path, branch);
                throw;
            }
        }

        public async Task<CreateBranchStatus> CreateBranchAsync(string repoOwner, string repoName, string branchName, string baseBranchName = "main", CancellationToken ct = default)
        {
            try
            {
                var branchExists = await IsExistingBranchAsync(repoOwner, repoName, branchName, ct);
                if (branchExists)
                {
                    return CreateBranchStatus.AlreadyExists;
                }

                // Get the base branch reference first
                var baseReference = await gitHubClient.Git.Reference.Get(repoOwner, repoName, $"heads/{baseBranchName}");

                // Create the new branch reference
                var newReference = new NewReference("refs/heads/" + branchName, baseReference.Object.Sha);
                var createdReference = await gitHubClient.Git.Reference.Create(repoOwner, repoName, newReference);

                return CreateBranchStatus.Created;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create branch {BranchName} in {RepoOwner}/{RepoName}", branchName, repoOwner, repoName);
                throw;
            }
        }

        public async Task<bool> IsExistingBranchAsync(string repoOwner, string repoName, string branchName, CancellationToken ct)
        {
            try
            {
                var branch = await gitHubClient.Repository.Branch.Get(repoOwner, repoName, branchName);
                return branch != null;
            }
            catch (NotFoundException)
            {
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting branch '{BranchName}' in {RepoOwner}/{RepoName}", branchName, repoOwner, repoName);
                return false;
            }
        }

        public async Task<RepositoryContent> GetContentsSingleAsync(string owner, string repoName, string path, string? branch = null, CancellationToken ct = default)
        {
            var contents = await GetContentsAsync(owner, repoName, path, branch, ct);
            if (contents == null || contents.Count == 0)
            {
                throw new InvalidOperationException($"Could not retrieve '{path}' file content");
            }
            if (string.IsNullOrEmpty(contents[0].Content))
            {
                throw new InvalidOperationException($"'{path}' file is empty");
            }
            return contents[0];
        }

        public async Task<HashSet<string>?> GetPublicOrgMembership(string username, CancellationToken ct)
        {
            var organizations = await gitHubClient.Organization.GetAllForUser(username);
            var userOrgs = organizations.Select(org => org.Login).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return userOrgs;
        }

        public async Task<bool> HasWritePermission(string owner, string repo, string username, CancellationToken ct)
        {
            try
            {
                var permission = await gitHubClient.Repository.Collaborator.ReviewPermission(owner, repo, username);

                // Write access to the Azure/azure-sdk-for-net repository is a sufficient proxy for knowing if the user has write permissions.
                return permission.Permission.Equals("write", StringComparison.OrdinalIgnoreCase) ||
                        permission.Permission.Equals("admin", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating permissions for user: {Username}", username);
                throw;
            }
        }

        public async Task<Octokit.SearchCodeResult> SearchFilesAsync(string searchQuery, CancellationToken ct)
        {
            var searchRequest = new Octokit.SearchCodeRequest(searchQuery);
            return await gitHubClient.Search.SearchCode(searchRequest);
        }
    }
}
