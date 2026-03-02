// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

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
    /// Names of other requirements that must be installed before this one can work.
    /// Uses <see cref="Name"/> strings (e.g., "Python", "Node.js", "Java").
    /// Default is empty (no dependencies).
    /// </summary>
    public virtual IReadOnlyList<string> DependsOn => [];

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
    /// Override RunCheck for custom validation logic.
    /// </summary>
    public virtual string[]? CheckCommand => null;

    /// <summary>
    /// Default timeout for check commands.
    /// Override in subclasses for commands that need more or less time.
    /// </summary>
    protected virtual TimeSpan CheckTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default timeout for install commands.
    /// Override in subclasses for installs that need more or less time.
    /// </summary>
    protected virtual TimeSpan InstallTimeout => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Helper to execute a command via the process helper with standard defaults.
    /// Subclasses that need custom ProcessOptions (e.g., PythonOptions) can call
    /// processHelper.Run() directly instead.
    /// </summary>
    protected async Task<ProcessResult> RunCommand(
        IProcessHelper processHelper,
        string[] command,
        RequirementContext ctx,
        CancellationToken ct,
        TimeSpan? timeout = null,
        bool logOutputStream = false)
    {
        var options = new ProcessOptions(
            command[0],
            args: command.Skip(1).ToArray(),
            timeout: timeout ?? CheckTimeout,
            logOutputStream: logOutputStream,
            workingDirectory: ctx.PackagePath
        );
        return await processHelper.Run(options, ct);
    }

    /// <summary>
    /// Runs the requirement check. Override for custom validation logic.
    /// </summary>
    /// <param name="processHelper">Process execution helper.</param>
    /// <param name="ctx">The current environment context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the requirement check.</returns>
    public virtual async Task<RequirementCheckOutput> RunCheck(
        IProcessHelper processHelper,
        RequirementContext ctx,
        CancellationToken ct = default)
    {
        if (CheckCommand == null || CheckCommand.Length == 0)
        {
            throw new InvalidOperationException(
                $"Requirement '{Name}' must define CheckCommand or override RunCheck");
        }

        var result = await RunCommand(processHelper, CheckCommand, ctx, ct);
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
    /// <see cref="RunInstall"/> — override this instead of duplicating logic in both.
    /// Returns null for non-auto-installable requirements.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>Array of commands, or null if not auto-installable.</returns>
    public virtual string[][]? GetInstallCommands(RequirementContext ctx) => null;

    /// <summary>
    /// Returns context-aware installation instructions as human-readable strings.
    /// When <see cref="IsAutoInstallable"/> is true, the default implementation derives
    /// instructions from <see cref="GetInstallCommands"/>.
    /// Non-auto-installable requirements must override this to provide manual instructions.
    /// </summary>
    /// <param name="ctx">The current environment context.</param>
    /// <returns>List of installation instructions.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requirement is not auto-installable and this method is not overridden.
    /// </exception>
    public virtual IReadOnlyList<string> GetInstructions(RequirementContext ctx)
    {
        var commands = GetInstallCommands(ctx);
        if (commands != null && commands.Length > 0)
        {
            return commands.Select(cmd => string.Join(" ", cmd)).ToList();
        }

        if (!IsAutoInstallable)
        {
             throw new InvalidOperationException(
            $"Requirement '{Name}' is not auto-installable and must override GetInstructions to provide manual install instructions.");
        }
        else
        {
            throw new InvalidOperationException(
            $"Requirement '{Name}' is auto-installable but GetInstallCommands did not return any commands to execute.");
        }
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
    /// <param name="processHelper">Process execution helper.</param>
    /// <param name="ctx">The current environment context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the install operation.</returns>
    public virtual async Task<RequirementCheckOutput> RunInstall(
        IProcessHelper processHelper,
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
                $"Requirement '{Name}' must define GetInstallCommands or override RunInstall");
        }

        foreach (var command in commands)
        {
            var result = await RunCommand(processHelper, command, ctx, ct, InstallTimeout, logOutputStream: true);
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
