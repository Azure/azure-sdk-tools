// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;
using LibGit2Sharp;


namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IGitHelper
    {
        // Get the owner 
        public Task<string> GetRepoOwnerNameAsync(string pathInRepo, bool findUpstreamParent = true);
        public Task<string> GetRepoFullNameAsync(string pathInRepo, bool findUpstreamParent = true);
        public Uri GetRepoRemoteUri(string pathInRepo);
        public string GetBranchName(string pathInRepo);
        public string GetMergeBaseCommitSha(string pathInRepo, string targetBranch);
        public string DiscoverRepoRoot(string pathInRepo);
        public string GetRepoName(string pathInRepo);
    }

    public class GitHelper(IGitHubService gitHubService, ILogger<GitHelper> logger) : IGitHelper
    {
        private readonly ILogger<GitHelper> logger = logger;
        private readonly IGitHubService gitHubService = gitHubService;

        /// <summary>
        /// Gets the SHA of the merge base (common ancestor) between the current branch and the target branch.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="targetBranchName">The name of the target branch to find the merge base with</param>
        /// <returns>The SHA of the merge base commit, or empty string if not found</returns>
        public string GetMergeBaseCommitSha(string pathInRepo, string targetBranchName)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo);
            using (var repo = new Repository(repoRoot))
            {
                // Get the current branch
                Branch currentBranch = repo.Head;
                var targetBranch = repo.Branches[targetBranchName];

                // Find the merge base (common ancestor) (git merge-base main HEAD)
                var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(currentBranch.Tip, targetBranch.Tip);
                logger.LogDebug(
                    "Git merge base analysis - Current branch: {currentBranch}, Target branch SHA: {mergeBaseCommitSha}",
                    currentBranch.FriendlyName,
                    mergeBaseCommit?.Sha);
                return mergeBaseCommit?.Sha ?? "";
            }
        }

        /// <summary>
        /// Gets the friendly name of the current branch in the repository.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The friendly name of the current branch</returns>
        public string GetBranchName(string pathInRepo)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo);
            using var repo = new Repository(repoRoot);
            var branchName = repo.Head.FriendlyName;
            return branchName;
        }

        /// <summary>
        /// Gets the remote origin URI of the repository in HTTPS format.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The HTTPS URI of the remote origin</returns>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine remote URL</exception>
        public Uri GetRepoRemoteUri(string pathInRepo)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo);
            using var repo = new Repository(repoRoot);
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

        /// <summary>
        /// Gets the owner name of the repository, optionally finding the upstream parent if the repo is a fork.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="findUpstreamParent">Whether to find the upstream parent repo if this is a fork (default: true)</param>
        /// <returns>The owner name of the repository or its upstream parent</returns>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository owner</exception>
        public async Task<string> GetRepoOwnerNameAsync(string pathInRepo, bool findUpstreamParent = true)
        {
            var uri = GetRepoRemoteUri(pathInRepo);
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
                logger.LogDebug("Parent repo URL: {parentRepoUrl}", parentRepoUrl);
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

        /// <summary>
        /// Gets the full name of the repository in the format "{owner}/{name}", e.g. "Azure/azure-rest-api-specs".
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="findUpstreamParent">Whether to find the upstream parent repo if this is a fork (default: true)</param>
        /// <returns>The full name of the repository in "owner/name" format</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        public async Task<string> GetRepoFullNameAsync(string pathInRepo, bool findUpstreamParent = true)
        {
            if (!string.IsNullOrEmpty(pathInRepo))
            {
                var repoOwner = await GetRepoOwnerNameAsync(pathInRepo, findUpstreamParent);
                var repoName = GetRepoName(pathInRepo);
                return $"{repoOwner}/{repoName}";
            }

            throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
        }

        /// <summary>
        /// Discovers and returns the root directory path of the git repository containing the specified path.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The absolute path to the repository root directory</returns>
        /// <exception cref="InvalidOperationException">Thrown when no git repository is found at or above the specified path</exception>
        public string DiscoverRepoRoot(string pathInRepo)
        {
            // Discover the repo root for this path
            var repoPath = Repository.Discover(pathInRepo);
            if (string.IsNullOrEmpty(repoPath))
            {
                throw new InvalidOperationException($"No git repository found at or above the path: {pathInRepo}");
            }

            // Repository.Discover returns the path to .git directory
            // The repository root is the parent directory of .git
            var gitDir = new DirectoryInfo(repoPath);
            if (gitDir.Parent == null || string.IsNullOrEmpty(gitDir.Parent.FullName))
            {
                throw new InvalidOperationException("Unable to determine repository root");
            }
            
            return gitDir.Parent.FullName;
        }

        /// <summary>
        /// Gets the repository name from the remote origin URL.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The name of the repository (without the owner)</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository name from remote URL</exception>
        public string GetRepoName(string pathInRepo)
        {
            if (string.IsNullOrEmpty(pathInRepo))
            {
                throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
            }
            
            var uri = GetRepoRemoteUri(pathInRepo);
            var segments = uri.Segments;

            if (segments.Length < 2)
            {
                throw new InvalidOperationException($"Unable to parse repository name from remote URL: {uri}");
            }

            string repoName = segments[^1].Replace(".git", "");
            return repoName;
        }
    }
}
