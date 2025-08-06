using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Tools.GeneratorAgent.Security;
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
        private readonly BuildErrorAnalyzer ErrorAnalyzer;
        private readonly string SdkOutputDir;

        public SdkBuildService(
            ILogger<SdkBuildService> logger,
            ProcessExecutor processExecutor,
            BuildErrorAnalyzer errorAnalyzer,
            string sdkOutputDir)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutor);
            ArgumentNullException.ThrowIfNull(errorAnalyzer);
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkOutputDir);

            Logger = logger;
            ProcessExecutor = processExecutor;
            ErrorAnalyzer = errorAnalyzer;
            SdkOutputDir = sdkOutputDir;
        }

        public async Task<SdkBuildResult> BuildSdkAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Starting SDK build in directory: {SdkOutputDir}", SdkOutputDir);

            string buildTarget = DetermineBuildTarget(SdkOutputDir);
            if (string.IsNullOrEmpty(buildTarget))
            {
                string errorMessage = $"No solution (.sln) or project (.csproj) files found in: {SdkOutputDir}";
                Logger.LogError(errorMessage);
                return SdkBuildResult.Failure(errorMessage);
            }

            try
            {
                string arguments = $"build \"{buildTarget}\"";
                
                Result<string> argValidation = InputValidator.ValidateProcessArguments(arguments);
                if (argValidation.IsFailure)
                {
                    return SdkBuildResult.Failure($"Build arguments validation failed: {argValidation.Error}");
                }

                Result buildResult = await ProcessExecutor.ExecuteAsync(
                    SecureProcessConfiguration.DotNetExecutable,
                    argValidation.Value,
                    SdkOutputDir,
                    cancellationToken).ConfigureAwait(false);

                if (buildResult.IsSuccess)
                {
                    Logger.LogInformation("SDK build completed successfully");
                    return SdkBuildResult.Success(buildResult.Output);
                }
                else
                {
                    Logger.LogError("Build error: {Error}", buildResult.Error);
                    Logger.LogError("Build output: {Output}", buildResult.Output);
                    
                    return SdkBuildResult.Failure(buildResult.Error, buildResult.Output);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return SdkBuildResult.Failure($"SDK build failed with exception: {ex.Message}", exception: ex);
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

    /// <summary>
    /// Represents the result of an SDK build operation.
    /// </summary>
    public class SdkBuildResult
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }
        public string Output { get; }
        public Exception? Exception { get; }

        private SdkBuildResult(bool isSuccess, string error, string output = "", Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Output = output;
            Exception = exception;
        }

        public static SdkBuildResult Success(string output = "") => new(true, string.Empty, output);
        
        public static SdkBuildResult Failure(string error, string output = "") 
            => new(false, error, output);
        
        public static SdkBuildResult Failure(string error, Exception exception) 
            => new(false, error, string.Empty, exception);
        
        public static SdkBuildResult Failure(Exception exception) 
            => new(false, exception.Message, string.Empty, exception);

        /// <summary>
        /// Throws the original exception if one was captured, otherwise throws InvalidOperationException
        /// </summary>
        public void ThrowIfFailure()
        {
            if (IsFailure)
            {
                if (Exception != null)
                {
                    throw Exception;
                }
                throw new InvalidOperationException(Error);
            }
        }
    }
}
