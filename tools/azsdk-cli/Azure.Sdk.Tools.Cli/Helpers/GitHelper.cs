// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;
using LibGit2Sharp;


namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IGitHelper
    {
        // Get the owner 
        public Task<string> GetRepoOwnerNameAsync(string path, bool findUpstreamParent = true);
        public Uri GetRepoRemoteUri(string path);
        public string GetBranchName(string path);
        public string GetMergeBaseCommitSha(string path, string targetBranch);
        public string DiscoverRepoRoot(string path);
        public string GetRepoName(string path);
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
                logger.LogDebug($"Git merge base analysis - Current branch: {currentBranch.FriendlyName}, Target branch SHA: {mergeBaseCommit?.Sha}");
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
                var url = ConvertSshToHttpsUrl(remote.Url);
                return new Uri(url);
            }
            throw new InvalidOperationException("Unable to determine remote URL.");
        }

        /// <summary>
        /// Converts SSH GitHub URLs to HTTPS format
        /// </summary>
        /// <param name="gitUrl">The Git URL (SSH or HTTPS)</param>
        /// <returns>HTTPS formatted Git URL</returns>
        private static string ConvertSshToHttpsUrl(string gitUrl)
        {
            if (string.IsNullOrEmpty(gitUrl))
            {
                return gitUrl;
            }

            // If it's already HTTPS, return as-is
            if (gitUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return gitUrl;
            }

            // Handle GitHub SSH URLs (e.g., git@github.com:Azure/azure-rest-api-specs.git)
            if (gitUrl.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                // Convert SSH URL to HTTPS URL
                // git@github.com:Azure/azure-rest-api-specs.git -> https://github.com/Azure/azure-rest-api-specs.git
                return gitUrl.Replace("git@github.com:", "https://github.com/");
            }

            // Return as-is if it's not a recognized format
            return gitUrl;
        }

        public async Task<string> GetRepoOwnerNameAsync(string path, bool findUpstreamParent = true)
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

            if (findUpstreamParent)
            {
                // Check if the repo is a fork and get the parent repo
                var parentRepoUrl = await gitHubService.GetGitHubParentRepoUrlAsync(repoOwner, repoName);
                logger.LogDebug($"Parent repo URL: {parentRepoUrl}");
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
    }
}
