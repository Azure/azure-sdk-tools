// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Telemetry;


namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IGitHelper
    {
        // Get the owner
        public Task<string> GetRepoOwnerNameAsync(string pathInRepo, bool findUpstreamParent = true, CancellationToken ct = default);
        public Task<string> GetRepoFullNameAsync(string pathInRepo, bool findUpstreamParent = true, CancellationToken ct = default);
        public Task<Uri> GetRepoRemoteUriAsync(string pathInRepo, CancellationToken ct);
        public Task<string> GetBranchNameAsync(string pathInRepo, CancellationToken ct);
        public Task<string> GetMergeBaseCommitShaAsync(string pathInRepo, string targetBranch, CancellationToken ct);
        public Task<string> DiscoverRepoRootAsync(string pathInRepo, CancellationToken ct);
        public Task<string> GetRepoNameAsync(string pathInRepo, CancellationToken ct);
        public Task<List<string>> GetChangedFilesAsync(string repoRoot, string targetCommitish, string sourceCommitish, string? diffPath, string diffFilterType, CancellationToken ct);
    }

    public class GitHelper(IGitHubService gitHubService, IGitCommandHelper gitCommandHelper, ILogger<GitHelper> logger) : IGitHelper
    {
        private readonly ILogger<GitHelper> logger = logger;
        private readonly IGitHubService gitHubService = gitHubService;
        private readonly IGitCommandHelper gitCommandHelper = gitCommandHelper;

        /// <summary>
        /// Gets the SHA of the merge base (common ancestor) between the current branch and the target branch.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="targetBranchName">The name of the target branch to find the merge base with</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The SHA of the merge base commit, or empty string if not found or on error</returns>
        public async Task<string> GetMergeBaseCommitShaAsync(string pathInRepo, string targetBranchName, CancellationToken ct)
        {
            var repoRoot = await DiscoverRepoRootAsync(pathInRepo, ct);
            var options = new GitOptions($"merge-base HEAD {targetBranchName}", repoRoot);
            var result = await gitCommandHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Command '{options.ShortName}' failed: {result.Output.Trim()}");
            }

            var mergeBaseSha = result.Stdout.Trim();
            var currentBranch = await GetBranchNameAsync(pathInRepo, ct);
            logger.LogDebug(
                "Git merge base - Current branch: {currentBranch}, Merge base SHA: {mergeBaseCommitSha}",
                currentBranch,
                mergeBaseSha);
            return mergeBaseSha;
        }

        /// <summary>
        /// Gets the friendly name of the current branch in the repository.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The friendly name of the current branch</returns>
        public async Task<string> GetBranchNameAsync(string pathInRepo, CancellationToken ct)
        {
            var repoRoot = await DiscoverRepoRootAsync(pathInRepo, ct);
            var options = new GitOptions("rev-parse --abbrev-ref HEAD", repoRoot);
            var result = await gitCommandHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Command '{options.ShortName}' failed: {result.Output.Trim()}");
            }

            return result.Stdout.Trim();
        }

        /// <summary>
        /// Gets the remote origin URI of the repository in HTTPS format.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The HTTPS URI of the remote origin</returns>
        public async Task<Uri> GetRepoRemoteUriAsync(string pathInRepo, CancellationToken ct)
        {
            var repoRoot = await DiscoverRepoRootAsync(pathInRepo, ct);
            var options = new GitOptions("remote get-url origin", repoRoot);
            var result = await gitCommandHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Command '{options.ShortName}' failed: {result.Output.Trim()}");
            }

            var remoteUrl = result.Stdout.Trim();
            var url = ConvertSshToHttpsUrl(remoteUrl);
            return new Uri(url);
        }

        /// <summary>
        /// Gets the owner name of the repository, optionally finding the upstream parent if the repo is a fork.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="findUpstreamParent">Whether to find the upstream parent repo if this is a fork (default: true)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The owner name of the repository or its upstream parent</returns>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository owner</exception>
        public async Task<string> GetRepoOwnerNameAsync(string pathInRepo, bool findUpstreamParent = true, CancellationToken ct = default)
        {
            var uri = await GetRepoRemoteUriAsync(pathInRepo, ct);
            var segments = uri.Segments;
            string repoOwner = string.Empty;
            string repoName = string.Empty;
            if (segments.Length > 2)
            {
                repoOwner = segments[^2].TrimEnd('/');
                repoName = segments[^1].Replace(".git", "");
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
        /// <param name="ct">Cancellation token</param>
        /// <returns>The full name of the repository in "owner/name" format</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        public async Task<string> GetRepoFullNameAsync(string pathInRepo, bool findUpstreamParent = true, CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(pathInRepo))
            {
                var repoOwner = await GetRepoOwnerNameAsync(pathInRepo, findUpstreamParent, ct);
                var repoName = await GetRepoNameAsync(pathInRepo, ct);
                return $"{repoOwner}/{repoName}";
            }

            throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
        }

        /// <summary>
        /// Discovers and returns the root directory path of the git repository containing the specified path.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The absolute path to the repository root directory</returns>
        /// <exception cref="InvalidOperationException">Thrown when no git repository is found at or above the specified path</exception>
        public async Task<string> DiscoverRepoRootAsync(string pathInRepo, CancellationToken ct)
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
                var options = new GitOptions("rev-parse --show-toplevel", workingDir);
                var result = await gitCommandHelper.Run(options, ct);

                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Git command failed: {result.Output.Trim()}");
                }

                var repoRoot = result.Stdout.Trim();
                var repoDirectoryName = Path.GetFileName(repoRoot);
                TelemetryPathSanitizer.AddAllowlistedSegment(repoDirectoryName);

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
        /// <param name="ct">Cancellation token</param>
        /// <returns>The name of the repository (without the owner)</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository name from remote URL</exception>
        public async Task<string> GetRepoNameAsync(string pathInRepo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathInRepo))
            {
                throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
            }

            var uri = await GetRepoRemoteUriAsync(pathInRepo, ct);
            var segments = uri.Segments;

            if (segments.Length < 2)
            {
                throw new InvalidOperationException($"Unable to parse repository name from remote URL: {uri}");
            }

            string repoName = segments[^1].Replace(".git", "");
            return repoName;
        }

        /// <summary>
        /// Gets the list of changed files between two commits using git diff.
        /// </summary>
        /// <param name="repoRoot">The root directory of the git repository</param>
        /// <param name="targetCommitish">The target commit/branch to diff against</param>
        /// <param name="sourceCommitish">The source commit/branch</param>
        /// <param name="diffPath">Optional path to limit the diff scope</param>
        /// <param name="diffFilterType">Git diff filter type (e.g., "d" for non-deleted, "D" for deleted only)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of changed file paths relative to the repository root</returns>
        public async Task<List<string>> GetChangedFilesAsync(
            string repoRoot,
            string targetCommitish,
            string sourceCommitish,
            string? diffPath,
            string diffFilterType,
            CancellationToken ct)
        {
            var args = new List<string>
            {
                "-c", "core.quotepath=off",
                "-c", "i18n.logoutputencoding=utf-8",
                "diff",
                $"{targetCommitish}...{sourceCommitish}",
                "--name-only",
                $"--diff-filter={diffFilterType}"
            };

            if (!string.IsNullOrEmpty(diffPath))
            {
                args.Add("--");
                args.Add(diffPath);
            }

            var options = new GitOptions([.. args], repoRoot, logOutputStream: false);
            var result = await gitCommandHelper.Run(options, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"git diff failed: {result.Output}");
            }

            return result.Stdout
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();
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
    }
}
