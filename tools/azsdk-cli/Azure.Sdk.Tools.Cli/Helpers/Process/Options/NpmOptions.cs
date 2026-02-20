// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public class NpmOptions : ProcessOptions, IProcessOptions
{
    private const string NPM = "npm";

    public string? Prefix { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(Prefix) ? "npm" : $"npm-{Path.GetFileName(Prefix)}";
            }
            return shortName;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NpmOptions"/> class for running 'npm exec' commands with an optional prefix.
    /// </summary>
    /// <param name="prefix">The npm prefix path. If provided, runs 'npm exec --prefix {prefix} -- {args}'. 
    /// If null or empty, runs 'npm exec -- {args}'.</param>
    /// <param name="args">The arguments to pass after the 'npm exec --' separator.</param>
    /// <param name="logOutputStream">Whether to log the output stream. Defaults to true.</param>
    /// <param name="workingDirectory">The working directory for the command. Defaults to current directory.</param>
    /// <param name="timeout">The timeout for the command. Defaults to 2 minutes.</param>
    public NpmOptions(
        string? prefix,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : this(BuildArgs(prefix, args), logOutputStream, workingDirectory, timeout)
    {
        Prefix = prefix;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NpmOptions"/> class for running any npm command.
    /// </summary>
    /// <param name="args">The full arguments to pass to npm (e.g., ["install"], ["run", "build"], ["ci"]).</param>
    /// <param name="logOutputStream">Whether to log the output stream. Defaults to true.</param>
    /// <param name="workingDirectory">The working directory for the command. Defaults to current directory.</param>
    /// <param name="timeout">The timeout for the command. Defaults to 2 minutes.</param>
    public NpmOptions(
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : base("npm", args, logOutputStream, workingDirectory, timeout) {}

    private static string[] BuildArgs(string? prefix, string[] args)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return ["exec", "--", .. args];
        }
        return ["exec", "--prefix", prefix, "--", .. args];
    }
}
