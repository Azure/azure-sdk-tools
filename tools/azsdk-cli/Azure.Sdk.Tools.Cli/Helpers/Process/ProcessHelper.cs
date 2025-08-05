// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers.Process
{
    public interface IProcessHelper
    {
        public ProcessResult RunProcess(string command, string[] args, string workingDirectory);
        public ProcessResult RunProcess(string command, string[] args, string workingDirectory, int processTimeout);
    }

    public class ProcessHelper(ILogger<ProcessHelper> logger) : IProcessHelper
    {

        private readonly int DefaultProcessTimeoutMs = 120_000; // Default timeout of 2 minutes

        private readonly ILogger<ProcessHelper> logger = logger;

        /// <summary>
        /// Runs a process with the specified command and arguments in the given working directory.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="args">The arguments to pass to the command.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <returns>A <see cref="ProcessResult"/> containing the output and exit code of the process.</returns>
        /// <remarks>
        /// The default timeout for the process is set to 120 seconds (2 minutes).
        /// If the process does not complete within this time, it will be terminated.
        /// </remarks>
        public ProcessResult RunProcess(string command, string[] args, string workingDirectory)
        {
            return RunProcess(command, args, workingDirectory, DefaultProcessTimeoutMs);
        }

        /// <summary>
        /// Runs a process with the specified command and arguments in the given working directory.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="args">The arguments to pass to the command.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <param name="processTimeout">The timeout in milliseconds for the process to complete.</param>
        /// <returns>A <see cref="ProcessResult"/> containing the output and exit code of the process.</returns>
        /// <remarks>
        /// If the process does not complete within the specified timeout, it will be terminated.
        /// </remarks>
        public ProcessResult RunProcess(string command, string[] args, string workingDirectory, int processTimeout)
        {
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
            int exitCode = -1;

            using (var process = new System.Diagnostics.Process())
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

                logger.LogDebug($"Running command: {command} {string.Join(" ", args)} in {workingDirectory}");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit(processTimeout);
                exitCode = process.ExitCode;

                if (process.ExitCode != 0)
                {
                    output.Append($"{Environment.NewLine}Process failed.");
                }
            }

            return new ProcessResult { Output = output.ToString(), ExitCode = exitCode };
        }
    }

    /// <summary>
    /// Result of running a process.
    /// </summary>
    public class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
    }
}
