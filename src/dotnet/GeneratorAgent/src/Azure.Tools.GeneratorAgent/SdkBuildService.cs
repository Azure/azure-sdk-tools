using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Security;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service responsible for building the generated SDK and capturing build logs.
    /// </summary>
    internal class SdkBuildService
    {
        private readonly ILogger<SdkBuildService> Logger;
        private readonly ProcessExecutor ProcessExecutor;
        private readonly string SdkOutputDir;

        public SdkBuildService(
            ILogger<SdkBuildService> logger,
            ProcessExecutor processExecutor,
            string sdkOutputDir)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutor);
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkOutputDir);

            Logger = logger;
            ProcessExecutor = processExecutor;
            SdkOutputDir = sdkOutputDir;
        }

        public async Task<Result<object>> BuildSdkAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting SDK build in directory: {SdkOutputDir}", SdkOutputDir);

            string buildTarget = DetermineBuildTarget(SdkOutputDir);
            if (string.IsNullOrEmpty(buildTarget))
            {
                throw new FileNotFoundException($"No solution (.sln) or project (.csproj) files found in: {SdkOutputDir}");
            }

            try
            {
                string arguments = $"build \"{buildTarget}\"";
                
                Result<string> argValidation = InputValidator.ValidateProcessArguments(arguments);
                if (argValidation.IsFailure)
                {
                    throw new ArgumentException($"Build arguments validation failed: {argValidation.Exception?.Message}");
                }

                Result<object> buildResult = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    argValidation.Value!,
                    SdkOutputDir,
                    cancellationToken).ConfigureAwait(false);

                if (buildResult.IsFailure && buildResult.ProcessException != null)
                {
                    return Result<object>.Failure(
                        new DotNetBuildException(
                            buildResult.ProcessException.Command,
                            buildResult.ProcessException.Output,
                            buildResult.ProcessException.Error,
                            buildResult.ProcessException.ExitCode ?? -1,
                            buildResult.ProcessException));
                }

                return buildResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogCritical(ex, "Unexpected system error during SDK build");
                throw;
            }
        }

        /// <summary>
        /// Determines the best build target (solution or project file) in the SDK output directory.
        /// </summary>
        /// <param name="sdkOutputDir">The SDK output directory to search</param>
        /// <returns>The path to the build target, or null if none found</returns>
        private string DetermineBuildTarget(string sdkOutputDir)
        {
            try
            {
                string[] solutionFiles = Directory.GetFiles(sdkOutputDir, "*.sln", SearchOption.TopDirectoryOnly);
                if (solutionFiles.Length > 0)
                {
                    string solutionFile = solutionFiles[0];
                    Logger.LogInformation("Found solution file: {SolutionFile}", solutionFile);
                    return solutionFile;
                }

                // If no solution file, look for project files in src directory (common pattern)
                string srcDirectory = Path.Combine(sdkOutputDir, "src");
                if (Directory.Exists(srcDirectory))
                {
                    string[] projectFiles = Directory.GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories);
                    if (projectFiles.Length > 0)
                    {
                        string projectFile = projectFiles[0];
                        Logger.LogInformation("Found project file in src: {ProjectFile}", projectFile);
                        return projectFile;
                    }
                }

                // If no solution file, look for any project files in the root directory
                string[] rootProjectFiles = Directory.GetFiles(sdkOutputDir, "*.csproj", SearchOption.TopDirectoryOnly);
                if (rootProjectFiles.Length > 0)
                {
                    string projectFile = rootProjectFiles[0];
                    Logger.LogInformation("Found project file in root: {ProjectFile}", projectFile);
                    return projectFile;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while determining build target in {SdkOutputDir}", sdkOutputDir);
                return string.Empty;
            }
        }
    }
}
