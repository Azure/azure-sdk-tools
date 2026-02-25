// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Result of running a requirement check.
/// </summary>
public class RequirementCheckOutput
{
    public bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Base class for all environment requirements that can be verified by the setup tool.
/// </summary>
public abstract class Requirement
{
    /// <summary>
    /// Display name (e.g., "Node.js").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Determines whether this requirement should be checked in the given context.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>True if the requirement should be checked, false otherwise.</returns>
    public virtual bool ShouldCheck(RequirementContext ctx) => true;

    /// <summary>
    /// Optional minimum version (e.g., "22.16.0"). Null if no specific version required.
    /// </summary>
    public virtual string? MinVersion => null;

    /// <summary>
    /// Command to run to verify the requirement is installed.
    /// Override RunCheckAsync for custom validation logic.
    /// </summary>
    public virtual string[]? CheckCommand => null;

    /// <summary>
    /// Runs the requirement check. Override for custom validation logic.
    /// The runCommand delegate handles execution details (timeout, working directory, etc.)
    /// </summary>
    /// <param name="runCommand">Delegate to execute a command and return the result.</param>
    /// <param name="ctx">The current environment context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the requirement check.</returns>
    public virtual async Task<RequirementCheckOutput> RunCheckAsync(
        Func<string[], Task<ProcessResult>> runCommand,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        if (CheckCommand == null || CheckCommand.Length == 0)
        {
            throw new InvalidOperationException(
                $"Requirement '{Name}' must define CheckCommand or override RunCheckAsync");
        }

        var result = await runCommand(CheckCommand);
        return new RequirementCheckOutput
        {
            Success = result.ExitCode == 0,
            Output = result.Output?.Trim(),
            Error = result.ExitCode != 0 ? result.Output?.Trim() : null
        };
    }

    /// <summary>
    /// Returns context-aware installation instructions.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>List of installation instructions.</returns>
    public abstract IReadOnlyList<string> GetInstructions(RequirementContext ctx);

    /// <summary>
    /// Optional reason why the requirement is needed.
    /// </summary>
    public virtual string? Reason => null;
}
