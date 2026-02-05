// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

/// <summary>
/// Specifies when workspace cleanup should occur.
/// </summary>
public enum CleanupPolicy
{
    /// <summary>
    /// Always cleanup the workspace after execution.
    /// </summary>
    Always,

    /// <summary>
    /// Never cleanup the workspace after execution.
    /// </summary>
    Never,

    /// <summary>
    /// Only cleanup the workspace if the execution was successful.
    /// </summary>
    OnSuccess
}

/// <summary>
/// Manages repository caching and workspace creation for benchmark execution.
/// Uses bare git clones for caching and worktrees for isolated workspaces.
/// </summary>
public class WorkspaceManager
{
    private readonly string _repoCachePath;
    private readonly string _workspacePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManager"/> class.
    /// Uses environment variables for configuration, with sensible defaults.
    /// </summary>
    /// <remarks>
    /// Environment variable overrides:
    /// <list type="bullet">
    /// <item><c>AZSDK_BENCHMARKS_REPO_CACHE</c>: Path for bare repository clones (default: ~/.cache/azsdk-benchmarks/repos)</item>
    /// <item><c>AZSDK_BENCHMARKS_WORKSPACE_DIR</c>: Path for worktree workspaces (default: /tmp/azsdk-benchmarks/workspaces)</item>
    /// </list>
    /// </remarks>
    public WorkspaceManager()
    {
        _repoCachePath = Environment.GetEnvironmentVariable("AZSDK_BENCHMARKS_REPO_CACHE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "azsdk-benchmarks", "repos");
        _workspacePath = Environment.GetEnvironmentVariable("AZSDK_BENCHMARKS_WORKSPACE_DIR")
            ?? Path.Combine(Path.GetTempPath(), "azsdk-benchmarks", "workspaces");
    }

    /// <summary>
    /// Prepares a workspace for benchmark execution by ensuring the repository is cached
    /// and creating an isolated worktree.
    /// </summary>
    /// <param name="repo">The repository configuration specifying which repo to clone.</param>
    /// <param name="scenarioId">A unique identifier for the benchmark scenario.</param>
    /// <returns>A <see cref="Workspace"/> instance pointing to the prepared worktree.</returns>
    /// <exception cref="InvalidOperationException">Thrown when git operations fail.</exception>
    public async Task<Workspace> PrepareAsync(RepoConfig repo, string scenarioId)
    {
        // Ensure bare clone exists or fetch if it does
        var barePath = await EnsureBareCloneAsync(repo);

        // Create a unique run ID using timestamp
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

        // Create workspace root path (contains all repos for this run)
        var workspaceRoot = Path.Combine(_workspacePath, scenarioId, runId);
        Directory.CreateDirectory(workspaceRoot);

        // Create worktree path as a subdirectory named after the repo
        var worktreePath = Path.Combine(workspaceRoot, repo.Name);

        // Create worktree from bare clone at the specified ref
        await CreateWorktreeAsync(barePath, worktreePath, repo.Ref);

        return new Workspace(workspaceRoot, repo.Name);
    }

    /// <summary>
    /// Cleans up a workspace based on the specified policy and execution result.
    /// </summary>
    /// <param name="workspace">The workspace to clean up.</param>
    /// <param name="policy">The cleanup policy to apply.</param>
    /// <param name="passed">Whether the benchmark execution was successful.</param>
    public async Task CleanupAsync(Workspace workspace, CleanupPolicy policy, bool passed)
    {
        var shouldCleanup = policy switch
        {
            CleanupPolicy.Always => true,
            CleanupPolicy.Never => false,
            CleanupPolicy.OnSuccess => passed,
            _ => false
        };

        if (!shouldCleanup)
        {
            return;
        }

        try
        {
            // Remove the worktree from git's tracking (worktree is at RepoPath, not RootPath)
            await RemoveWorktreeAsync(workspace.RepoPath);

            // Delete the workspace root directory (contains all repos for this run)
            if (Directory.Exists(workspace.RootPath))
            {
                Directory.Delete(workspace.RootPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - cleanup failures shouldn't fail the benchmark
            Console.Error.WriteLine($"Warning: Failed to cleanup workspace at {workspace.RootPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures a bare clone of the repository exists in the cache, fetching updates if it already exists.
    /// </summary>
    private async Task<string> EnsureBareCloneAsync(RepoConfig repo)
    {
        var barePath = Path.Combine(_repoCachePath, repo.EffectiveOwner, repo.Name + ".git");

        if (Directory.Exists(barePath))
        {
            // Fetch latest changes (shallow to stay consistent with initial clone)
            await RunGitCommandAsync(barePath, "fetch", "--all", "--prune", "--depth=1");
        }
        else
        {
            // Create parent directory and clone
            Directory.CreateDirectory(Path.GetDirectoryName(barePath)!);
            await RunGitCommandAsync(
                Path.GetDirectoryName(barePath)!,
                "clone", "--bare", "--depth=1", repo.CloneUrl, barePath);
        }

        return barePath;
    }

    /// <summary>
    /// Creates a worktree from a bare clone at the specified ref.
    /// </summary>
    private async Task CreateWorktreeAsync(string barePath, string worktreePath, string gitRef)
    {
        // Fetch the specific ref to ensure it's available (needed for shallow clones)
        await RunGitCommandAsync(barePath, "fetch", "origin", gitRef, "--depth=1");

        // Create the worktree
        await RunGitCommandAsync(barePath, "worktree", "add", worktreePath, "FETCH_HEAD", "--detach");
    }

    /// <summary>
    /// Removes a worktree from git's tracking.
    /// </summary>
    private async Task RemoveWorktreeAsync(string worktreePath)
    {
        // Find the bare repo that owns this worktree by looking for .git file
        var gitFile = Path.Combine(worktreePath, ".git");
        if (!File.Exists(gitFile))
        {
            return;
        }

        // The .git file contains a path to the actual git directory
        var gitContent = await File.ReadAllTextAsync(gitFile);
        if (!gitContent.StartsWith("gitdir:"))
        {
            return;
        }

        // Extract the git directory path and find the parent bare repo
        var gitDirPath = gitContent.Substring("gitdir:".Length).Trim();
        var bareRepoPath = Path.GetFullPath(Path.Combine(gitDirPath, "..", ".."));

        if (Directory.Exists(bareRepoPath))
        {
            await RunGitCommandAsync(bareRepoPath, "worktree", "remove", worktreePath, "--force");
        }
    }

    /// <summary>
    /// Runs a git command and throws if it fails.
    /// </summary>
    private static async Task RunGitCommandAsync(string workingDirectory, params string[] args)
    {
        var arguments = string.Join(" ", args.Select(EscapeArgument));

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {process.ExitCode}: git {arguments}\n" +
                $"Working directory: {workingDirectory}\n" +
                $"Output: {output}\n" +
                $"Error: {error}");
        }
    }

    /// <summary>
    /// Escapes a command-line argument if it contains special characters.
    /// </summary>
    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\\'))
        {
            return $"\"{arg.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        return arg;
    }
}
