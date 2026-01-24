// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Options for running git commands via the git CLI.
/// </summary>
public class GitOptions : ProcessOptions, IProcessOptions
{
    private const string GIT = "git";

    /// <summary>
    /// The git subcommand being run (e.g., "rev-parse", "remote", "merge-base").
    /// </summary>
    public string SubCommand { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(SubCommand) ? "git" : $"git {SubCommand}";
            }
            return shortName;
        }
    }

    /// <summary>
    /// Creates options for running a git command.
    /// </summary>
    /// <param name="args">Arguments to pass to git (including subcommand)</param>
    /// <param name="workingDirectory">Working directory for the git command</param>
    /// <param name="logOutputStream">Whether to log output streams</param>
    /// <param name="timeout">Optional timeout (defaults to 2 minutes from base class)</param>
    public GitOptions(
        string[] args,
        string workingDirectory,
        bool logOutputStream = false,
        TimeSpan? timeout = null
    ) : base(GIT, args, logOutputStream, workingDirectory, timeout)
    {
        SubCommand = args.Length > 0 ? args[0] : string.Empty;
    }

    /// <summary>
    /// Creates options for running a git command from a single argument string.
    /// </summary>
    /// <param name="arguments">Space-separated arguments to pass to git (e.g., "rev-parse --show-toplevel")</param>
    /// <param name="workingDirectory">Working directory for the git command</param>
    /// <param name="logOutputStream">Whether to log output streams</param>
    /// <param name="timeout">Optional timeout (defaults to 2 minutes from base class)</param>
    public GitOptions(
        string arguments,
        string workingDirectory,
        bool logOutputStream = false,
        TimeSpan? timeout = null
    ) : this(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries), workingDirectory, logOutputStream, timeout)
    {
    }
}
