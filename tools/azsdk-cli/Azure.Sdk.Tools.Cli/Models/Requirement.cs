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
/// Common reasons why a requirement cannot be auto-installed.
/// </summary>
public static class NotInstallableReasons
{
    public const string LanguageRuntime = "Language runtime must be installed manually";
    public const string SystemTool = "System-level tool must be installed manually";
    public const string BundledWithLanguage = "Bundled with its language runtime — install the runtime first";
    public const string BuildTool = "Build tool must be installed manually";
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
    /// Returns context-aware structured install commands for auto-installation.
    /// Each element is a single command represented as a string array (program + args).
    /// This is the single source of truth for both <see cref="GetInstructions"/> and
    /// <see cref="RunInstallAsync"/> — override this instead of duplicating logic in both.
    /// Returns null for non-auto-installable requirements.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>Array of commands, or null if not auto-installable.</returns>
    public virtual string[][]? GetInstallCommands(RequirementContext ctx) => null;

    /// <summary>
    /// Returns context-aware installation instructions as human-readable strings.
    /// Default implementation derives instructions from <see cref="GetInstallCommands"/>.
    /// Override for non-auto-installable requirements or when instructions need
    /// additional prose beyond the install commands.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>List of installation instructions.</returns>
    public virtual IReadOnlyList<string> GetInstructions(RequirementContext ctx)
    {
        var commands = GetInstallCommands(ctx);
        if (commands != null && commands.Length > 0)
        {
            return commands.Select(cmd => string.Join(" ", cmd)).ToList();
        }
        return [];
    }

    /// <summary>
    /// Optional reason why the requirement is needed.
    /// </summary>
    public virtual string? Reason => null;

    /// <summary>
    /// Whether this requirement can be automatically installed.
    /// Defaults to false; auto-installable requirements override this to true.
    /// </summary>
    public virtual bool IsAutoInstallable => false;

    /// <summary>
    /// Reason why this requirement cannot be auto-installed, if applicable.
    /// Use constants from <see cref="NotInstallableReasons"/>.
    /// Returns null when <see cref="IsAutoInstallable"/> is true.
    /// </summary>
    public virtual string? NotAutoInstallableReason => null;

    /// <summary>
    /// Runs the auto-installation for this requirement. Override for custom install logic
    /// (e.g., venv handling, working-directory changes).
    /// The default implementation executes each command from <see cref="GetInstallCommands"/> sequentially,
    /// short-circuiting on the first failure.
    /// </summary>
    /// <param name="runCommand">Delegate to execute a command and return the result.</param>
    /// <param name="ctx">The current environment context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the install operation.</returns>
    public virtual async Task<RequirementCheckOutput> RunInstallAsync(
        Func<string[], Task<ProcessResult>> runCommand,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        if (!IsAutoInstallable)
        {
            return new RequirementCheckOutput
            {
                Success = false,
                Error = NotAutoInstallableReason ?? $"Requirement '{Name}' is not auto-installable"
            };
        }

        var commands = GetInstallCommands(ctx);
        if (commands == null || commands.Length == 0)
        {
            throw new InvalidOperationException(
                $"Requirement '{Name}' must define GetInstallCommands or override RunInstallAsync");
        }

        foreach (var command in commands)
        {
            var result = await runCommand(command);
            if (result.ExitCode != 0)
            {
                return new RequirementCheckOutput
                {
                    Success = false,
                    Output = result.Output?.Trim(),
                    Error = result.Output?.Trim()
                };
            }
        }

        return new RequirementCheckOutput
        {
            Success = true,
            Output = $"{Name} installed successfully"
        };
    }
}
