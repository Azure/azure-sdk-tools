// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public class NpxOptions : ProcessOptions, IProcessOptions
{
    private const string NPX = "npx";

    public string? Package { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(Package) ? "npx" : Package;
            }
            return shortName;
        }
    }

    public NpxOptions(
        string? package,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : this(BuildArgs(package, args), logOutputStream, workingDirectory, timeout)
    {
        Package = package;
    }

    private NpxOptions(
        string[] args,
        bool logOutputStream,
        string? workingDirectory,
        TimeSpan? timeout
    ) : base("npx", args, logOutputStream, workingDirectory, timeout) {}

    private static string[] BuildArgs(string? package, string[] args)
    {
        if (string.IsNullOrEmpty(package))
        {
            return args;
        }
        return ["--yes", $"--package={package}", "--", .. args];
    }
}
