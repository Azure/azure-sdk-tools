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

        public virtual CommandResult Run(GitAssetsConfiguration config, string arguments)
        {
            ProcessStartInfo processStartInfo = CreateGitProcessInfo(config);
            processStartInfo.Arguments = arguments;

            try
            {
                // TODO: verbose logging add here
                Console.WriteLine($"git {processStartInfo.Arguments}");
                var process = Process.Start(processStartInfo);
                string output = process.StandardOutput.ReadToEnd();
                string errorOutput = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CommandResult()
                {
                    ReturnCode = process.ExitCode,
                    StdErr = output,
                    StdOut = errorOutput
                };
            }
            catch (Exception e)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"{e.Message}");
            }
        }

    }
}
