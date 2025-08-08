// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Graph.Drives.Item.Items.Item.GetActivitiesByInterval;
using Microsoft.Graph.Models.TermStore;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IProcessHelper
    {
        public IProcessHelper CreateForCrossPlatform(string linuxCommand, string[] linuxArgs, string windowsCommand, string[] windowsArgs, string workingDirectory);
        public Task<ProcessResult> RunProcess(CancellationToken ct = default);
        public Task<ProcessResult> RunProcess(TimeSpan timeout, CancellationToken ct = default);
        public Task<ProcessResult> RunProcess(string command, string[] args, string workingDirectory, CancellationToken ct = default);
        public Task<ProcessResult> RunProcess(string command, string[] args, string workingDirectory, TimeSpan timeout, CancellationToken ct = default);
    }

    public class ProcessHelper(ILogger<ProcessHelper> logger) : IProcessHelper
    {

        private const int DEFAULT_PROCESS_TIMEOUT_SECONDS = 120;  // Default timeout of 2 minutes

        private readonly ILogger<ProcessHelper> logger = logger;

        private bool _initializedForCrossPlatform { get; set; } = false;
        private string _unixCommand { get; set; }
        private string[] _unixArgs { get; set; }
        private string _windowsCommand { get; set; }
        private string[] _windowsArgs { get; set; }
        private string _workingDirectory { get; set; }

        public IProcessHelper CreateForCrossPlatform(string unixCommand, string[] unixArgs, string windowsCommand, string[] windowsArgs, string workingDirectory)
        {
            return new ProcessHelper(logger)
            {
                _initializedForCrossPlatform = true,
                _unixCommand = unixCommand,
                _unixArgs = unixArgs,
                _windowsCommand = windowsCommand,
                _windowsArgs = windowsArgs,
                _workingDirectory = workingDirectory
            };
        }

        public async Task<ProcessResult> RunProcess(CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(DEFAULT_PROCESS_TIMEOUT_SECONDS);
            return await RunProcess(timeout, ct);
        }

        public async Task<ProcessResult> RunProcess(TimeSpan timeout, CancellationToken ct)
        {
            if (!_initializedForCrossPlatform)
            {
                throw new InvalidOperationException("ProcessHelper is not properly initialized. Call CreateForCrossPlatform() first.");
            }

            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _windowsCommand : _unixCommand;
            var args = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? _windowsArgs : _unixArgs;
            return await RunProcess(command, args, _workingDirectory, timeout, ct);
        }

        public async Task<ProcessResult> RunProcess(string command, string[] args, string workingDirectory, CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(DEFAULT_PROCESS_TIMEOUT_SECONDS);
            return await RunProcess(command, args, workingDirectory, timeout, ct);
        }

        /// <summary>
        /// Runs a process with the specified command and arguments in the given working directory.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="args">The arguments to pass to the command.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <param name="timeout">The timeout for the process to complete.</param>
        /// <param name="ct">The cancellation token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ProcessResult"/> containing the output and exit code of the process.</returns>
        /// <remarks>
        /// If the process does not complete within the specified timeout, it will be terminated.
        /// </remarks>
        public async Task<ProcessResult> RunProcess(string command, string[] args, string workingDirectory, TimeSpan timeout, CancellationToken ct)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                args = ["/C", command, .. args];
                command = "cmd.exe";
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                processStartInfo.ArgumentList.Add(arg);
            }

            var output = new StringBuilder();
            int exitCode = 1;

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        output.AppendLine(e.Data);
                    }
                };

                logger.LogDebug("Running command: {command} {args} in {workingDirectory}", command, string.Join(" ", args), workingDirectory);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    output.AppendLine();
                    output.AppendLine($"Process '{command}' timed out after {timeout.TotalMilliseconds}ms");
                    logger.LogError("Process '{command}' timed out after {timeout}ms", command, timeout.TotalMilliseconds);
                    return new ProcessResult { Output = output.ToString(), ExitCode = 124 };
                }
                catch (Exception ex)
                {
                    output.AppendLine();
                    output.AppendLine($"Process '{command}' failed with exception: {ex.Message}");
                    logger.LogError(ex, "Process '{command}' failed", command);
                    return new ProcessResult { Output = output.ToString(), ExitCode = 1 };
                }

                exitCode = process.ExitCode;
                if (process.ExitCode != 0)
                {
                    output.AppendLine($"Process '{command}' failed with exit code {process.ExitCode}");
                }
            }

            return new ProcessResult { Output = output.ToString() ?? "", ExitCode = exitCode };
        }
    }

    /// <summary>
    /// Result of running a process.
    /// </summary>
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
    }
}
