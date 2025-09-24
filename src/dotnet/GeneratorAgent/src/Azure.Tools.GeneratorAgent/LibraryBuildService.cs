using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Service responsible for building the generated Library and capturing build logs.
    /// </summary>
    internal class LibraryBuildService
    {
        private readonly ILogger<LibraryBuildService> Logger;
        private readonly ProcessExecutionService ProcessExecutionService;
        private readonly string SdkOutputDir;

        public LibraryBuildService(
            ILogger<LibraryBuildService> logger,
            ProcessExecutionService processExecutionService,
            string sdkOutputDir)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutionService);
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkOutputDir);

            Logger = logger;
            ProcessExecutionService = processExecutionService;
            SdkOutputDir = sdkOutputDir;
        }

        public async Task<Result<object>> BuildSdkAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Starting library build in directory: {SdkOutputDir}", SdkOutputDir);

            Result<string> buildTargetResult = DetermineBuildTarget(SdkOutputDir);
            if (buildTargetResult.IsFailure)
            {
                return Result<object>.Failure(buildTargetResult.Exception!);
            }

            string arguments = $"build \"{buildTargetResult.Value}\"";
            
            Result<string> argValidation = InputValidator.ValidateProcessArguments(arguments);
            if (argValidation.IsFailure)
            {
                return Result<object>.Failure(argValidation.Exception!);
            }

            Result<object> buildResult = await ProcessExecutionService.ExecuteAsync(
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

        /// <summary>
        /// Determines the best build target (solution or project file) in the SDK output directory.
        /// </summary>
        /// <param name="sdkOutputDir">The SDK output directory to search</param>
        /// <returns>Result containing the path to the build target, or failure if none found</returns>
        private Result<string> DetermineBuildTarget(string sdkOutputDir)
        {
            try
            {
                string[] solutionFiles = Directory.GetFiles(sdkOutputDir, "*.sln", SearchOption.TopDirectoryOnly);
                if (solutionFiles.Length > 0)
                {
                    string solutionFile = solutionFiles[0];
                    Logger.LogDebug("Found solution file: {SolutionFile}", solutionFile);
                    return Result<string>.Success(solutionFile);
                }

                // If no solution file, look for project files in src directory (common pattern)
                string srcDirectory = Path.Combine(sdkOutputDir, "src");
                if (Directory.Exists(srcDirectory))
                {
                    string[] projectFiles = Directory.GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories);
                    if (projectFiles.Length > 0)
                    {
                        string projectFile = projectFiles[0];

                            Logger.LogDebug("Found project file in src: {ProjectFile}", projectFile);

                        return Result<string>.Success(projectFile);
                    }
                }

                // If no solution file, look for any project files in the root directory
                string[] rootProjectFiles = Directory.GetFiles(sdkOutputDir, "*.csproj", SearchOption.TopDirectoryOnly);
                if (rootProjectFiles.Length > 0)
                {
                    string projectFile = rootProjectFiles[0];

                    Logger.LogDebug("Found project file in root: {ProjectFile}", projectFile);
                    
                    return Result<string>.Success(projectFile);
                }

                return Result<string>.Failure(new FileNotFoundException($"No solution (.sln) or project (.csproj) files found in: {sdkOutputDir}"));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while determining build target in {SdkOutputDir}", sdkOutputDir);
                return Result<string>.Failure(ex);
            }
        }
    }
}
