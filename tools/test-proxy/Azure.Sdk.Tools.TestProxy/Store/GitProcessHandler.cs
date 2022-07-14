using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Store
{

    public class CommandResult
    {
        public int ReturnCode;
        public string StdErr;
        public string StdOut;
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
                    ReturnCode = process.ExitCode,
                    StdErr = stdOut,
                    StdOut = stdErr
                };

            }
            catch (Exception e)
            {
                result = new CommandResult()
                {
                    ReturnCode = -1,
                    CommandException = e
                };
            }

            if (result.ReturnCode != 0)
            {
                return false;
            }

            return true;
        }

    }
}
