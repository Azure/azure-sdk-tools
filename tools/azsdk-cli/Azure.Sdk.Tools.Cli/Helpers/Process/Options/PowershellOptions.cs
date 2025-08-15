// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class PowershellOptions : ProcessOptions, IProcessOptions
{
    public string? ScriptPath { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(ScriptPath) ? "pwsh" : Path.GetFileName(ScriptPath);
            }
            return shortName;
        }
    }

    public PowershellOptions(
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : base("pwsh", ["-Command", .. args], logOutputStream, workingDirectory, timeout) { }

    public PowershellOptions(
        string scriptPath,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
    ) : base("pwsh", ["-File", scriptPath, .. args], logOutputStream, workingDirectory, timeout)
    {
        ScriptPath = scriptPath;
    }
}
