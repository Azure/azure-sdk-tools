// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IProcessHelper
    {
        public ProcessResult RunProcess(string command, string[] args, string workingDirectory);
    }

    public class ProcessHelper(ILogger<ProcessHelper> logger) : IProcessHelper
    {

        private readonly ILogger<ProcessHelper> logger = logger;

        public ProcessResult RunProcess(string command, string[] args, string workingDirectory)
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

                logger.LogInformation($"Running command: {command} {string.Join(" ", args)} in {workingDirectory}");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit(100_000);
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
