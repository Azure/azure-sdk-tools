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

        public virtual bool TryRun(GitAssetsConfiguration config, string arguments, out CommandResult result)
        {
            ProcessStartInfo processStartInfo = CreateGitProcessInfo(config);
            processStartInfo.Arguments = arguments;

            try
            {
                // TODO: verbose logging add here
                Console.WriteLine($"git {processStartInfo.Arguments}");
                var process = Process.Start(processStartInfo);
                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                int returnCode = process.ExitCode;

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
