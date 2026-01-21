// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;


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

    public class GitHelper(IGitHubService gitHubService, IGitCommandHelper gitCommandHelper, ILogger<GitHelper> logger) : IGitHelper
    {
        private readonly ILogger<GitHelper> logger = logger;
        private readonly IGitHubService gitHubService = gitHubService;
        private readonly IGitCommandHelper gitCommandHelper = gitCommandHelper;

        /// <summary>
        /// Runs a git command and returns the trimmed stdout output.
        /// </summary>
        /// <param name="workingDirectory">The directory to run the git command in</param>
        /// <param name="arguments">The git command arguments (without 'git' prefix), space-separated</param>
        /// <returns>The trimmed stdout output from the git command</returns>
        /// <exception cref="InvalidOperationException">Thrown when git command fails</exception>
        private string RunGitCommand(string workingDirectory, string arguments)
        {
            logger.LogDebug("Running git command: git {arguments} in {workingDirectory}", arguments, workingDirectory);
            
            // Split arguments string into array for GitOptions
            var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var options = new GitOptions(args, workingDirectory);

            var result = gitCommandHelper.Run(options, CancellationToken.None).GetAwaiter().GetResult();

            if (result.ExitCode != 0)
            {
                logger.LogDebug("Git command failed with exit code {exitCode}: {output}", result.ExitCode, result.Output.Trim());
                throw new InvalidOperationException($"Git command failed: {result.Output.Trim()}");
            }

            logger.LogDebug("Git command succeeded: {stdout}", result.Stdout.Trim());
            return result.Stdout.Trim();
        }

        /// <summary>
        /// Gets the SHA of the merge base (common ancestor) between the current branch and the target branch.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="targetBranchName">The name of the target branch to find the merge base with</param>
        /// <returns>The SHA of the merge base commit, or empty string if not found or on error</returns>
        public string GetMergeBaseCommitSha(string pathInRepo, string targetBranchName)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo);
            try
            {
                var mergeBaseSha = RunGitCommand(repoRoot, $"merge-base HEAD {targetBranchName}");
                var currentBranch = GetBranchName(pathInRepo);
                logger.LogDebug(
                    "Git merge base - Current branch: {currentBranch}, Merge base SHA: {mergeBaseCommitSha}",
                    currentBranch,
                    mergeBaseSha);
                return mergeBaseSha;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Failed to find merge base with branch '{targetBranch}': {error}",
                    targetBranchName,
                    ex.Message);
                return "";
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
            return RunGitCommand(repoRoot, "rev-parse --abbrev-ref HEAD");
        }

        /// <summary>
        /// Gets the remote origin URI of the repository in HTTPS format.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The HTTPS URI of the remote origin</returns>
        public Uri GetRepoRemoteUri(string pathInRepo)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo);
            var remoteUrl = RunGitCommand(repoRoot, "remote get-url origin");
            var url = ConvertSshToHttpsUrl(remoteUrl);
            return new Uri(url);
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
            if (string.IsNullOrWhiteSpace(pathInRepo))
            {
                throw new InvalidOperationException("Path cannot be null or empty");
            }

            // Determine the working directory for git command
            // If path is a file, use its directory; if directory, use it directly
            string? workingDir;
            if (Directory.Exists(pathInRepo))
            {
                workingDir = pathInRepo;
            }
            else
            {
                // Path might be a file or non-existent path - get its directory
                workingDir = Path.GetDirectoryName(pathInRepo);
                
                // Handle edge cases like "C:" which returns null
                if (string.IsNullOrEmpty(workingDir))
                {
                    workingDir = Directory.GetCurrentDirectory();
                }
            }

            if (!Directory.Exists(workingDir))
            {
                throw new InvalidOperationException($"Directory does not exist: {workingDir} (from path: {pathInRepo})");
            }

            try
            {
                // git rev-parse --show-toplevel returns the root of the working tree
                // This correctly handles both normal repos and worktrees
                var repoRoot = RunGitCommand(workingDir, "rev-parse --show-toplevel");
                
                // Normalize path separators: git returns forward slashes on Windows
                return repoRoot.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"No git repository found at or above the path: {pathInRepo}. Details: {ex.Message}", ex);
            }
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
