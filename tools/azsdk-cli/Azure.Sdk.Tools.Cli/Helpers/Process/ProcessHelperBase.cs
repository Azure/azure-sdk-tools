// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Helpers;

public abstract class ProcessHelperBase<T>(ILogger<T> logger, IRawOutputHelper outputHelper)
{
    /// <summary>
    /// Runs a process with the specified command and arguments in the given working directory.
    /// </summary>
    /// <param name="options">The options for the process to run.</param>
    /// <param name="ct">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ProcessResult"/> containing the output and exit code of the process.</returns>
    /// <remarks>
    /// If the process does not complete within the specified timeout, it will be terminated.
    /// </remarks>
    protected async Task<ProcessResult> Run(IProcessOptions options, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in options.Args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        ProcessResult result = new() { ExitCode = 1 };

        using (var process = new Process())
        {
            process.StartInfo = processStartInfo;
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (result)
                    {
                        result.AppendStdout(e.Data);
                        if (options.LogOutputStream)
                        {
                            outputHelper.OutputConsole($"[{options.ShortName}] {e.Data}");
                        }
                    }
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    lock (result)
                    {
                        result.AppendStderr(e.Data);
                        if (options.LogOutputStream)
                        {
                            outputHelper.OutputConsoleError($"[{options.ShortName}] {e.Data}");
                        }
                    }
                }
            };

            // Notify if the command might take a while
            var timeoutMessage = options.Timeout > ProcessOptions.DEFAULT_PROCESS_TIMEOUT
                                    ? $"with timeout {options.Timeout.TotalMinutes} minutes "
                                    : "";

            logger.LogInformation(
                "Running command [{command} {args}] {timeoutMessage}in {workingDirectory}",
                options.Command,
                string.Join(" ", options.Args),
                timeoutMessage,
                options.WorkingDirectory);

            process.Start();
            lock (result)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            try
            {
                tryPrintSeparator(options.LogOutputStream);
                await process.WaitForExitAsync(linkedCts.Token);
                tryPrintSeparator(options.LogOutputStream);
            }
            // Insert a more descriptive error message when the task times out
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogError("Process '{command}' timed out after {timeout}ms", options.ShortName, options.Timeout.TotalMilliseconds);
                throw new OperationCanceledException($"Process '{options.ShortName}' timed out after {options.Timeout.TotalMilliseconds}ms");
            }

            result.ExitCode = process.ExitCode;
        }

        return result;
    }

    private void tryPrintSeparator(bool logOutputStream)
    {
        try
        {
            var windowWidth = Console.WindowWidth;
            var separatorLength = 80;
            if (windowWidth < 80)
            {
                separatorLength = 10;
            }
            outputHelper.OutputConsole(new string('-', separatorLength));
        }
        catch { }
    }
}

public enum StdioLevel
{
    StandardOutput,
    StandardError,
}
