// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Validation;

/// <summary>
/// Defines a validator that checks whether a benchmark scenario passed or failed.
/// </summary>
public interface IValidator
{
    /// <summary>
    /// Gets the human-readable name of this validator.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates the scenario result against specific criteria.
    /// </summary>
    /// <param name="context">The validation context containing execution results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(
        ValidationContext context, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of running a single validator.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets the name of the validator that produced this result.
    /// </summary>
    public required string ValidatorName { get; init; }

    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets a human-readable message describing the result.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets additional details (command output, diff, etc.).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Gets the duration of the validation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a passing result.
    /// </summary>
    public static ValidationResult Pass(string validatorName, string? message = null) =>
        new() { ValidatorName = validatorName, Passed = true, Message = message };

    /// <summary>
    /// Creates a failing result.
    /// </summary>
    public static ValidationResult Fail(string validatorName, string message, string? details = null) =>
        new() { ValidatorName = validatorName, Passed = false, Message = message, Details = details };
}

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
    public IReadOnlyList<string> ToolCalls { get; init; } = [];

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
