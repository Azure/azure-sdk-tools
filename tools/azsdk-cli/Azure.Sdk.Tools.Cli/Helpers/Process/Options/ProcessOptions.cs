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
}

public class ProcessOptions : IProcessOptions
{
    public static readonly TimeSpan DEFAULT_PROCESS_TIMEOUT = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan maxProcessTimeout = TimeSpan.FromHours(2);

    public const string CMD = "cmd.exe";

    public string Command { get; }
    public List<string> Args { get; } = [];
    public string WorkingDirectory { get; }
    public TimeSpan Timeout { get; }
    public bool LogOutputStream { get; }

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
        TimeSpan? timeout = null
    ) : this(command, args, command, args, logOutputStream, workingDirectory, timeout) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessOptions"/> class that uses the same command-line arguments on both platforms.
    /// See <see cref="ProcessOptions(string, string[], string, string[], bool, string?, TimeSpan?)"/> to pass different arguments 
    /// for each platform.
    /// </summary>
    public ProcessOptions(
        string unixCommand,
        string windowsCommand,
        string[] args,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null)
        : this(unixCommand, args, windowsCommand, args, logOutputStream, workingDirectory, timeout)
    { }

    public ProcessOptions(
        string unixCommand,
        string[] unixArgs,
        string windowsCommand,
        string[] windowsArgs,
        bool logOutputStream = true,
        string? workingDirectory = null,
        TimeSpan? timeout = null
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
            args = ["/C", windowsCommand, .. windowsArgs];
            command = CMD;
        }

        this.Command = command;
        this.Args = [.. args];
        this.WorkingDirectory = workingDirectory;

        if (timeout > maxProcessTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "timeout cannot exceed 2 hours.");
        }

        this.Timeout = timeout ?? DEFAULT_PROCESS_TIMEOUT;
        this.LogOutputStream = logOutputStream;
    }

    public void AddArgs(params string[] args)
    {
        Args.AddRange(args);
    }
}
