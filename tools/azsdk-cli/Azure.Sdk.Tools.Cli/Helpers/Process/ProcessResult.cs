// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Result of running a process.
/// </summary>
public class ProcessResult
{
    public int ExitCode { get; set; }
    public string Output { get => string.Join(Environment.NewLine, OutputDetails.Select(x => x.Item2)); }
    public List<(StdioLevel, string)> OutputDetails { get; set; } = [];

    public void AppendStdout(string line)
    {
        OutputDetails.Add((StdioLevel.StandardOutput, line));
    }

    public void AppendStderr(string line)
    {
        OutputDetails.Add((StdioLevel.StandardError, line));
    }
}
