using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Store
{

    public class CommandResult
    {
        public int ExitCode;
        public string StdErr;
        public string StdOut;
        public string Arguments;
        public Exception CommandException;
    }

    /// <summary>
    /// This class offers an easy wrapper abstraction for shelling out to git.
    /// </summary>
    public class GitProcessHandler
    {
        public virtual ProcessStartInfo CreateGitProcessInfo(GitAssetsConfiguration config)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = config.AssetsRepoLocation,
            };

            startInfo.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH");

            return startInfo;
        }

        /// <summary>
        /// Invokes git.exe against a GitAssetsConfiguration.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="arguments"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public virtual bool TryRun(GitAssetsConfiguration config, string arguments, out CommandResult result)
        {
            ProcessStartInfo processStartInfo = CreateGitProcessInfo(config);
            processStartInfo.Arguments = arguments;

            try
            {
                DebugLogger.LogInformation($"git {arguments}");
                var process = Process.Start(processStartInfo);
                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                int returnCode = process.ExitCode;

                DebugLogger.LogDebug($"StdOut: {stdOut}");
                DebugLogger.LogDebug($"StdErr: {stdErr}");
                DebugLogger.LogDebug($"ExitCode: {process.ExitCode}");

                result = new CommandResult()
                {
                    ExitCode = process.ExitCode,
                    StdErr = stdOut,
                    StdOut = stdErr,
                    Arguments = arguments
                };
            }
            catch (Exception e)
            {
                DebugLogger.LogDebug(e.Message);

                result = new CommandResult()
                {
                    ExitCode = -1,
                    CommandException = e
                };
            }

            if (result.ExitCode != 0)
            {
                return false;
            }

            return true;
        }

    }
}
