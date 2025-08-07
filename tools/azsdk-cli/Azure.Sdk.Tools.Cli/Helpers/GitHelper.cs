// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;
using LibGit2Sharp;


namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IGitHelper
    {
        // Get the owner 
        public Task<string> GetRepoOwnerNameAsync(string path, bool findForkParent = true);
        public Uri GetRepoRemoteUri(string path);
        public string GetBranchName(string path);
        public string GetMergeBaseCommitSha(string path, string targetBranch);
        public string DiscoverRepoRoot(string path);
        public string GetRepoName(string path);
        public int ParsePullRequestNumberFromUrl(string prUrl);
        public string ParseRepoOwnerFromUrl(string prUrl);
    }
    public class GitHelper(IGitHubService gitHubService, ILogger<GitHelper> logger) : IGitHelper
    {
        private readonly ILogger<GitHelper> logger = logger;
        private readonly IGitHubService gitHubService = gitHubService;


        public string GetMergeBaseCommitSha(string path, string targetBranchName)
        {
            using (var repo = new Repository(path))
            {
                // Get the current branch
                Branch currentBranch = repo.Head;
                var targetBranch = repo.Branches[targetBranchName];

                // Find the merge base (common ancestor) (git merge-base main HEAD)
                var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, targetBranch.Tip);
                logger.LogInformation($"Current branch  :{currentBranch.FriendlyName}, Target branch SHA: {mergeBaseCommit?.Sha}");
                return mergeBaseCommit?.Sha ?? "";
            }
        }

        public string GetBranchName(string repoPath)
        {
            using var repo = new Repository(repoPath);
            var branchName = repo.Head.FriendlyName;
            return branchName;
        }

        public Uri GetRepoRemoteUri(string path)
        {
            using var repo = new Repository(path);
            var remote = repo.Network?.Remotes["origin"];
            if (remote != null)
            {
                return new Uri(remote.Url);
            }
            throw new InvalidOperationException("Unable to determine remote URL.");
        }

        public async Task<string> GetRepoOwnerNameAsync(string path, bool findForkParent = true)
        {
            var uri = GetRepoRemoteUri(path);
            var segments = uri.Segments;
            string repoOwner = string.Empty;
            string repoName = string.Empty;
            if (segments.Length > 2)
            {
                repoOwner = segments[^2].TrimEnd('/');
                repoName = segments[^1].TrimEnd(".git".ToCharArray());
            }

            if(findForkParent) {
                // Check if the repo is a fork and get the parent repo
                var parentRepoUrl = await gitHubService.GetGitHubParentRepoUrlAsync(repoOwner, repoName);
                logger.LogInformation($"Parent repo URL: {parentRepoUrl}");
                if (!string.IsNullOrEmpty(parentRepoUrl))
                {
                    var parentSegments = new Uri(parentRepoUrl).Segments;
                    if (parentSegments.Length > 2)
                    {
                        repoOwner = parentSegments[^2].TrimEnd('/');
                    }
                }
            }

            if (!string.IsNullOrEmpty(repoOwner))
            {
                return repoOwner;
            }

            throw new InvalidOperationException("Unable to determine repository owner.");
        }        

        public string DiscoverRepoRoot(string path)
        {
            var repoPath = Repository.Discover(path);
            if (string.IsNullOrEmpty(repoPath))
            {
                throw new InvalidOperationException($"No git repository found at or above the path: {path}");
            }
            
            // Repository.Discover returns the path to .git directory
            // The repository root is the parent directory of .git
            var gitDir = new DirectoryInfo(repoPath);
            return gitDir.Parent?.FullName ?? throw new InvalidOperationException("Unable to determine repository root");
        }

        public string GetRepoName(string path)
        {
            var repoRoot = DiscoverRepoRoot(path);
            return new DirectoryInfo(repoRoot).Name ?? throw new InvalidOperationException($"Unable to determine repository name for path: {path}");
        }

        // Static helpers for parsing PR details from GitHub PR URLs
        public int ParsePullRequestNumberFromUrl(string prUrl)
        {
            var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"github\.com\/[^\/]+\/[^\/]+\/pull\/(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int prNumber))
            {
                return prNumber;
            }

            throw new InvalidOperationException($"Unable to parse pull request number from URL: {prUrl}.");
        }

        public string ParseRepoOwnerFromUrl(string prUrl)
        {
            var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"github\.com\/([^\/]+)\/[^\/]+\/pull\/\d+");
            return match.Success ? match.Groups[1].Value : throw new InvalidOperationException($"Unable to parse repo owner from URL: {prUrl}.");
        }
    }
}
