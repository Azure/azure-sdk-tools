// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Utility methods for git operations in tests.
/// Uses git CLI directly instead of LibGit2Sharp for better compatibility
/// with worktrees and to avoid native library dependencies.
/// </summary>
public static class GitTestHelper
{
    /// <summary>
    /// Initializes a new git repository in the specified directory.
    /// </summary>
    /// <param name="directory">The directory to initialize as a git repository</param>
    public static void GitInit(string directory)
    {
        RunGit(directory, "init");
        // Configure user for commits (required for git commit to work)
        RunGit(directory, "config user.email \"test@test.com\"");
        RunGit(directory, "config user.name \"Test User\"");
    }

    /// <summary>
    /// Adds a remote to the git repository.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="remoteName">The name of the remote (e.g., "origin")</param>
    /// <param name="url">The URL of the remote</param>
    public static void GitRemoteAdd(string directory, string remoteName, string url)
    {
        RunGit(directory, $"remote add {remoteName} {url}");
    }

    /// <summary>
    /// Creates a new branch in the git repository.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="branchName">The name of the branch to create</param>
    public static void GitCreateBranch(string directory, string branchName)
    {
        RunGit(directory, $"checkout -b {branchName}");
    }

    /// <summary>
    /// Stages all files and creates a commit.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="message">The commit message</param>
    public static void GitCommit(string directory, string message)
    {
        // Escape double quotes in the message
        var escapedMessage = string.IsNullOrEmpty(message) ? string.Empty : message.Replace("\"", "\\\"");
        RunGit(directory, "add -A");
        RunGit(directory, $"commit -m \"{escapedMessage}\" --allow-empty");
    }

    /// <summary>
    /// Switches to a branch.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="branchName">The name of the branch to switch to</param>
    public static void GitCheckout(string directory, string branchName)
    {
        RunGit(directory, $"checkout {branchName}");
    }

    /// <summary>
    /// Runs a git command in the specified directory.
    /// </summary>
    /// <param name="workingDirectory">The directory to run the command in</param>
    /// <param name="arguments">The git command arguments (without 'git' prefix)</param>
    /// <exception cref="InvalidOperationException">Thrown when the git command fails</exception>
    private static void RunGit(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true, // Prevent stdin blocking in test environments
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start git process for command: git {arguments}");
        }

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {process.ExitCode}: git {arguments}\n" +
                $"Working directory: {workingDirectory}\n" +
                $"Error: {stderr}");
        }
    }
}
