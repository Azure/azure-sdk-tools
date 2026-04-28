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

        // When a prefix is provided, resolve the binary from node_modules/.bin directly.
        // Using "npm exec --prefix <dir> -- <bin>" causes npm to try to fetch a package
        // named <bin> from the registry when .npmrc is specified, which fails when the binary name differs from
        // the npm package name (e.g., "tsp-client" binary from "@azure-tools/typespec-client-generator-cli").
        // Reading the package.json to find the actual providing package and adding --package= avoids this.
        var packageJsonPath = Path.Combine(prefix, "package.json");
        if (File.Exists(packageJsonPath))
        {
            var packageJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            if (packageJson.RootElement.TryGetProperty("dependencies", out var deps))
            {
                var packages = new List<string>();
                foreach (var dep in deps.EnumerateObject())
                {
                    packages.Add($"--package={dep.Name}");
                }
                if (packages.Count > 0)
                {
                    return ["exec", "--prefix", prefix, .. packages, "--", .. args];
                }
            }
        }

        return ["exec", "--prefix", prefix, "--", .. args];
    }
}
