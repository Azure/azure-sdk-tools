// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IProcessHelper
    {
        public Task<ProcessResult> RunProcessAsync(string command, string[] args, string workingDirectory, CancellationToken ct);
    }

    public class ProcessHelper(ILogger<ProcessHelper> logger) : IProcessHelper
    {
        private readonly ILogger<ProcessHelper> logger = logger;

        private ProcessStartInfo CreateProcessStartInfo(string command, string[] args, string workingDirectory)
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

            return processStartInfo;
        }

        /// <summary>
        /// Runs a process asynchronously with the specified command and arguments in the given working directory.
        /// </summary>
        /// <param name="command">The command to run.</param>
        /// <param name="args">The arguments to pass to the command.</param>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation, with a <see cref="ProcessResult"/> containing the output and exit code of the process.</returns>
        public async Task<ProcessResult> RunProcessAsync(string command, string[] args, string workingDirectory, CancellationToken ct)
        {
            var processStartInfo = CreateProcessStartInfo(command, args, workingDirectory);

            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return new ProcessResult
                {
                    ExitCode = -1,
                    Output = $"Failed to start {command}"
                };
            }

            await process.WaitForExitAsync(ct);


            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Process {command} exited with code {process.ExitCode}");
            }

            var output = new StringBuilder();
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            output.AppendLine(stdout);
            if (process.ExitCode != 0)
            {
                output.AppendLine(stderr);
                output.AppendLine($"Process failed.");
            }

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString()
            };
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
