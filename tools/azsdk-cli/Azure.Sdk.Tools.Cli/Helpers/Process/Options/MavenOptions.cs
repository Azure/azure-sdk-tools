// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public class MavenOptions : ProcessOptions, IProcessOptions
{
    private const string MVN = "mvn";

    public string? Goal { get; }
    public string? PomPath { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(Goal) ? MVN : Goal;
            }
            return shortName;
        }
    }

    public MavenOptions(
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IDictionary<string, string>? environmentVariables = null
    ) : base(MVN, args, logOutputStream, workingDirectory, timeout, environmentVariables)
    {
    }

    public MavenOptions(
        string goal,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IDictionary<string, string>? environmentVariables = null
    ) : base(MVN, [goal, .. args], logOutputStream, workingDirectory, timeout, environmentVariables)
    {
        Goal = goal;
    }

    public MavenOptions(
        string goal,
        string[] args,
        string pomPath,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IDictionary<string, string>? environmentVariables = null
    ) : base(MVN, [goal, .. args, "-f", pomPath], logOutputStream, workingDirectory, timeout, environmentVariables)
    {
        Goal = goal;
        PomPath = pomPath;
    }
}
