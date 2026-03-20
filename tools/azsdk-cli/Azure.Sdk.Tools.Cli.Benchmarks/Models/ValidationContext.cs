// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Models;

/// <summary>
/// Context provided to validators containing execution results and workspace state.
/// </summary>
public class ValidationContext
{
    // === WORKSPACE ===

    /// <summary>
    /// Gets the workspace containing the repository.
    /// </summary>
    public required Workspace Workspace { get; init; }

    /// <summary>
    /// Gets the path to the repository root within the workspace.
    /// Shortcut for Workspace.RepoPath.
    /// </summary>
    public string RepoPath => Workspace.RepoPath;

    // === GIT STATE ===

    /// <summary>
    /// Gets the git diff of all uncommitted changes (captured after agent execution).
    /// May be null if diff capture failed.
    /// </summary>
    public string? GitDiff { get; init; }

    /// <summary>
    /// Gets the git diff for the home repo only (for multi-repo scenarios).
    /// For single-repo scenarios, this is the same as GitDiff.
    /// </summary>
    public string? HomeRepoGitDiff { get; init; }

    /// <summary>
    /// Gets git diffs for all repos keyed by repo name (for multi-repo scenarios).
    /// </summary>
    public IReadOnlyDictionary<string, string> AllGitDiffs { get; init; } = 
        new Dictionary<string, string>();

    // === EXECUTION RESULTS ===

    /// <summary>
    /// Gets the tool calls made during execution.
    /// </summary>
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];

    /// <summary>
    /// Gets the conversation messages from the agent session.
    /// </summary>
    public IReadOnlyList<object> Messages { get; init; } = [];

    /// <summary>
    /// Gets whether the agent execution completed without errors.
    /// </summary>
    public bool ExecutionCompleted { get; init; }

    /// <summary>
    /// Gets any error message from execution.
    /// </summary>
    public string? ExecutionError { get; init; }

    // === SCENARIO METADATA ===

    /// <summary>
    /// Gets the scenario name (for logging/debugging).
    /// </summary>
    public string ScenarioName { get; init; } = "";
}
