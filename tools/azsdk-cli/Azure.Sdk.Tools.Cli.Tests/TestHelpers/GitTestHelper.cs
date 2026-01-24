// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Utility methods for git operations in tests.
/// Uses GitCommandHelper for git CLI execution.
/// </summary>
public static class GitTestHelper
{
    private static readonly IGitCommandHelper GitCommandHelper = new GitCommandHelper(
        NullLogger<GitCommandHelper>.Instance, 
        Mock.Of<IRawOutputHelper>());

    /// <summary>
    /// Initializes a new git repository in the specified directory.
    /// </summary>
    /// <param name="directory">The directory to initialize as a git repository</param>
    public static async Task GitInitAsync(string directory)
    {
        await GitCommandHelper.Run(new GitOptions("init", directory), CancellationToken.None);
        // Configure user for commits (required for git commit to work)
        await GitCommandHelper.Run(new GitOptions(["config", "user.email", "test@test.com"], directory), CancellationToken.None);
        await GitCommandHelper.Run(new GitOptions(["config", "user.name", "Test User"], directory), CancellationToken.None);
    }

    /// <summary>
    /// Adds a remote to the git repository.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="remoteName">The name of the remote (e.g., "origin")</param>
    /// <param name="url">The URL of the remote</param>
    public static async Task GitRemoteAddAsync(string directory, string remoteName, string url)
    {
        await GitCommandHelper.Run(new GitOptions($"remote add {remoteName} {url}", directory), CancellationToken.None);
    }

    /// <summary>
    /// Creates a new branch in the git repository.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="branchName">The name of the branch to create</param>
    public static async Task GitCreateBranchAsync(string directory, string branchName)
    {
        await GitCommandHelper.Run(new GitOptions($"checkout -b {branchName}", directory), CancellationToken.None);
    }

    /// <summary>
    /// Stages all files and creates a commit.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="message">The commit message</param>
    public static async Task GitCommitAsync(string directory, string message)
    {
        await GitCommandHelper.Run(new GitOptions("add -A", directory), CancellationToken.None);
        await GitCommandHelper.Run(new GitOptions(["commit", "-m", message, "--allow-empty"], directory), CancellationToken.None);
    }

    /// <summary>
    /// Switches to a branch.
    /// </summary>
    /// <param name="directory">The git repository directory</param>
    /// <param name="branchName">The name of the branch to switch to</param>
    public static async Task GitCheckoutAsync(string directory, string branchName)
    {
        await GitCommandHelper.Run(new GitOptions($"checkout {branchName}", directory), CancellationToken.None);
    }
}
