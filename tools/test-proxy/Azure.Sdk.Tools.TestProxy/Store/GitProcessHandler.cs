using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

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
        /// <summary>
        /// Internal class to hold the minimum supported version of git. If that
        /// version changes we only need to change it here.
        /// </summary>
        class GitMinVersion
        {
            // The minimum version of git supported is 2.37.0 due to cone/non-cone options for sparse-checkout
            public static int Major = 2;
            public static int Minor = 37;
            public static int Patch = 0;
            public static string minVersionString = $"{Major}.{Minor}.{Patch}";
        }

        /// <summary>
        /// Create a ProcessStartInfo that's exclusively used for execution of git commands
        /// </summary>
        /// <param name="workingDirectory">The directory where the commands are to be executed. For normal processing
        /// through GitStore, this will end up being GitAssetsConfiguration's AssetsRepoLocation.</param>
        /// <returns></returns>
        public virtual ProcessStartInfo CreateGitProcessInfo(string workingDirectory)
        {
            var startInfo = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };

            startInfo.EnvironmentVariables["PATH"] = Environment.GetEnvironmentVariable("PATH");

            return startInfo;
        }

        /// <summary>
        /// Invokes a git command. If it fails in any way, throws GitProcessException. Otherwise returns the result of the git invocation.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="inputCommands"></param>
        /// <returns>A list of command results. One can assume success in all of them unless an exception has been thrown</returns>
        /// <exception cref="GitProcessException">Throws GitProcessException on returnCode != 0 OR if an unexpected exception is thrown during invocation.</exception>
        public virtual IEnumerable<CommandResult> Run(GitAssetsConfiguration config, params string[] inputCommands)
        {
            List<CommandResult> results = new List<CommandResult>();

            foreach (var inputCommand in inputCommands)
            {
                results.Add(Run(inputCommand, config));
            }

            return results;
        }

        /// <summary>
        /// Invokes a git command. If it fails in any way, throws GitProcessException. Otherwise returns the result of the git invocation.
        /// </summary>
        /// <param name="arguments">git command line arguments</param>
        /// <param name="config">GitAssetsConfiguration</param>
        /// <returns></returns>
        /// <exception cref="GitProcessException">Throws GitProcessException on returnCode != 0 OR if an unexpected exception is thrown during invocation.</exception>
        public virtual CommandResult Run(string arguments, GitAssetsConfiguration config)
        {
            return Run(arguments, config.AssetsRepoLocation);
        }

        /// <summary>
        /// Invokes a git command. If it fails in any way, throws GitProcessException. Otherwise returns the result of the git invocation.
        /// </summary>
        /// <param name="arguments">git command line arguments</param>
        /// <param name="workingDirectory">effectively the AssetsRepoLocation</param>
        /// <returns></returns>
        /// <exception cref="GitProcessException">Throws GitProcessException on returnCode != 0 OR if an unexpected exception is thrown during invocation.</exception>
        public virtual CommandResult Run(string arguments, string workingDirectory)
        {
            ProcessStartInfo processStartInfo = CreateGitProcessInfo(workingDirectory);
            processStartInfo.Arguments = arguments;

            CommandResult result = new CommandResult()
            {
                Arguments = arguments
            };

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


                result.ExitCode = process.ExitCode;
                result.StdErr = stdErr;
                result.StdOut = stdOut;


                if (result.ExitCode == 0){
                    return result;
                }
                else
                {
                    throw new GitProcessException(result);
                }
            }
            catch (Exception e)
            {
                DebugLogger.LogDebug(e.Message);

                result.ExitCode = -1;
                result.CommandException = e;

                throw new GitProcessException(result);
            }
        }

        /// <summary>
        /// Invokes git binary against a GitAssetsConfiguration.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="arguments"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public virtual bool TryRun(string arguments, GitAssetsConfiguration config, out CommandResult result)
        {
            ProcessStartInfo processStartInfo = CreateGitProcessInfo(config.AssetsRepoLocation);
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
                    StdErr = stdErr,
                    StdOut = stdOut,
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

        /// <summary>
        /// Verify that the version of git running on the machine is greater equal the git minimum version. 
        /// This is more for the people running the CLI/TestProxy locally than for lab machines which seem
        /// to be running on the latest, released versions. The reason is that any git version less than 
        /// 2.37.0 won't have the cone/no-cone options used by sparse-checkout.
        /// </summary>
        /// <exception cref="GitProcessException">Thrown by the internal call to Run.</exception>
        /// <exception cref="GitVersionException">Thrown if the version doesn't meet the min or if we can't determine it.</exception>
        public void VerifyGitMinVersion(string testVersion=null)
        {
            string localGitVersion = testVersion;
            if (String.IsNullOrEmpty(testVersion))
            {
                // We need to run git --version to get the current version. The directory in which
                // the process executes is irrelevant.
                CommandResult result = Run("--version", Directory.GetCurrentDirectory());
                localGitVersion = result.StdOut.Trim();
            }
            // Sample git versions from the various platforms:
            // Windows: git version 2.37.2.windows.2
            // Mac: git version 2.32.1 (Apple Git-133)
            // Linux: git version 2.25.1
            // Regex to scrape major, minor and patch versions from the git version string
            Regex rx = new Regex(@"git version (?<Major>\d*)\.(?<Minor>\d*)(\.(?<Patch>\d*)?)?", RegexOptions.Compiled);
            Match match = rx.Match(localGitVersion);

            if (match.Success)
            {
                string gitVersionExceptionMessage = $"{localGitVersion} is less than the minimum supported Git version {GitMinVersion.minVersionString}";
                // match.Groups["Major"].Value, match.Groups["Minor"].Value, match.Groups["Patch"].Value
                int major = int.Parse(match.Groups["Major"].Value);
                int minor = int.Parse(match.Groups["Minor"].Value);
                int patch = int.Parse(match.Groups["Patch"].Value);
                if (major < GitMinVersion.Major)
                {
                    throw new GitVersionException(gitVersionExceptionMessage);
                }
                else if (major == GitMinVersion.Major)
                {
                    if (minor < GitMinVersion.Minor)
                    {
                        throw new GitVersionException(gitVersionExceptionMessage);
                    }
                    else if (minor == GitMinVersion.Minor)
                    {
                        if (patch < GitMinVersion.Patch)
                        {
                            throw new GitVersionException(gitVersionExceptionMessage);
                        }
                    }
                }
            }
            else
            {
                // In theory we shouldn't get here. If git isn't installed or on the path, the Run command should throw.
                throw new GitVersionException($"Unable to determine the local git version from the returned version string '{localGitVersion}'. Please ensure that git is installed on the machine and has been added to the PATH.");
            }    
        }
    }
}
