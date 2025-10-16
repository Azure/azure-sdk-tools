// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface IProcessOptions
{
    string Command { get; }
    List<string> Args { get; }
    string WorkingDirectory { get; }
    TimeSpan Timeout { get; }
    bool LogOutputStream { get; }
    string ShortName { get; }
    Dictionary<string, string>? EnvironmentVariables { get; }
}

public class ProcessOptions : IProcessOptions
{
    private const string CMD = "cmd.exe";
    private const int DEFAULT_PROCESS_TIMEOUT_SECONDS = 120;  // Default timeout of 2 minutes

    public string Command { get; }
    public List<string> Args { get; } = [];
    public string WorkingDirectory { get; }
    public TimeSpan Timeout { get; }
    public bool LogOutputStream { get; }
    public Dictionary<string, string>? EnvironmentVariables { get; }

    private string shortName;
    public virtual string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = Command == CMD ? (Args.FirstOrDefault() ?? "") : Command;
            }
            return shortName;
        }
    }

    public ProcessOptions(
        string command,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null
    ) : this(command, args, command, args, logOutputStream, workingDirectory, timeout, environmentVariables) { }

    public ProcessOptions(
        string unixCommand,
        string[] unixArgs,
        string windowsCommand,
        string[] windowsArgs,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null
    )
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Environment.CurrentDirectory;
        }

        var command = unixCommand;
        var args = unixArgs;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isWindows && windowsCommand != "pwsh" && windowsCommand != "powershell")
        {
            args = ["/C", command, .. windowsArgs];
            command = CMD;
        }

        this.Command = command;
        this.Args = [.. args];
        this.WorkingDirectory = workingDirectory;
        this.Timeout = timeout ?? TimeSpan.FromSeconds(DEFAULT_PROCESS_TIMEOUT_SECONDS);
        this.LogOutputStream = logOutputStream;
        this.EnvironmentVariables = environmentVariables;
    }

    public void AddArgs(params string[] args)
    {
        Args.AddRange(args);
    }
}
