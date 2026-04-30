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
    /// <param name="prefix">The npm prefix path. If provided, resolves the binary from {prefix}/node_modules/.bin
    /// and invokes it directly. If null or empty, runs 'npm exec -- {args}'.</param>
    /// <param name="args">The arguments to pass. The first element is the binary name (e.g., "tsp-client").</param>
    /// <param name="logOutputStream">Whether to log the output stream. Defaults to true.</param>
    /// <param name="workingDirectory">The working directory for the command. Defaults to current directory.</param>
    /// <param name="timeout">The timeout for the command. Defaults to 2 minutes.</param>
    public NpmOptions(
        string? prefix,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : base(
        ResolveBinCommand(prefix, args) ?? NPM,
        ResolveBinArgs(prefix, args),
        logOutputStream,
        workingDirectory,
        timeout)
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

    /// <summary>
    /// Resolves the binary command from node_modules/.bin when a prefix is provided
    /// and the binary exists locally. Returns null to fall back to npm.
    /// On Windows, prefers the .cmd shim; on Unix, uses the script directly.
    /// This is to solve the issue when '.npmrc' is specified, where npm would try to
    /// resolve the package from the feed but the command name is not the package name.
    /// </summary>
    private static string? ResolveBinCommand(string? prefix, string[] args)
    {
        if (!string.IsNullOrEmpty(prefix) && args.Length > 0)
        {
            var binName = args[0];
            var binDir = Path.Combine(prefix, "node_modules", ".bin");

            // On Windows, npm creates .cmd shims that cmd.exe can execute
            var cmdPath = Path.Combine(binDir, binName + ".cmd");
            if (File.Exists(cmdPath))
            {
                return cmdPath;
            }

            // On Unix, the script is directly executable
            var binPath = Path.Combine(binDir, binName);
            if (File.Exists(binPath))
            {
                return binPath;
            }
        }
        return null;
    }

    /// <summary>
    /// Builds the argument array. When the binary is resolved directly from
    /// node_modules/.bin, returns the remaining args (skipping the binary name).
    /// Otherwise, falls back to "npm exec" with the original args.
    /// </summary>
    private static string[] ResolveBinArgs(string? prefix, string[] args)
    {
        // If the binary can be resolved directly, return just the remaining args
        if (ResolveBinCommand(prefix, args) != null)
        {
            return args.Skip(1).ToArray();
        }

        // Fall back to npm exec
        if (string.IsNullOrEmpty(prefix))
        {
            return ["exec", "--", .. args];
        }
        return ["exec", "--prefix", prefix, "--", .. args];
    }
}
