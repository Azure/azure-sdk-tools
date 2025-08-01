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
        public string? FindRepositoryRoot(string startPath);
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

        /// <summary>
        /// Finds the repository root by looking for common repository indicators.
        /// This method first tries to use LibGit2Sharp for Git-based detection, then falls back to 
        /// directory structure-based detection for cases where .git might not be available.
        /// </summary>
        /// <param name="startPath">Path to start searching from</param>
        /// <returns>Repository root path or null if not found</returns>
        public string? FindRepositoryRoot(string startPath)
        {
            try
            {
                // First try using LibGit2Sharp which is the most reliable method for Git repositories
                return DiscoverRepoRoot(startPath);
            }
            catch (InvalidOperationException)
            {
                // If LibGit2Sharp fails, fall back to directory structure-based detection
                logger.LogDebug("Git-based repository detection failed, falling back to directory structure detection");
                
                var currentDir = new DirectoryInfo(startPath);
                
                while (currentDir != null)
                {
                    // Look for .git directory first as the most reliable indicator
                    if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
                    {
                        return currentDir.FullName;
                    }
                    
                    // Also check for common repository structure indicators
                    // Only consider it a repo root if it has both eng directory AND is likely the root
                    if (Directory.Exists(Path.Combine(currentDir.FullName, "eng")) &&
                        Directory.Exists(Path.Combine(currentDir.FullName, "sdk")))
                    {
                        return currentDir.FullName;
                    }
                    
                    currentDir = currentDir.Parent;
                }
                
                return null;
            }
        }
    }
}
