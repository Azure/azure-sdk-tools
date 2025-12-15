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

        public LibraryBuildService(
            ILogger<LibraryBuildService> logger,
            ProcessExecutionService processExecutionService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(processExecutionService);

            Logger = logger;
            ProcessExecutionService = processExecutionService;
        }

        public async Task<Result<object>> BuildSdkAsync(string sdkOutputDir, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sdkOutputDir);
            Logger.LogDebug("Starting library build in directory: {SdkOutputDir}", sdkOutputDir);

            string buildTargetResult = DetermineBuildTarget(sdkOutputDir);

            string arguments = $"build \"{buildTargetResult}\"";
            
            string validatedArguments = InputValidator.ValidateProcessArguments(arguments);

            return await ProcessExecutionService.ExecuteAsync(
                SecureProcessConfiguration.DotNetExecutable,
                validatedArguments,
                sdkOutputDir,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Determines the best build target (solution or project file) in the SDK output directory.
        /// </summary>
        /// <param name="sdkOutputDir">The SDK output directory to search</param>
        /// <returns>Result containing the path to the build target, or failure if none found</returns>
        private string DetermineBuildTarget(string sdkOutputDir)
        {
            try
            {
                string[] solutionFiles = Directory.GetFiles(sdkOutputDir, "*.sln", SearchOption.TopDirectoryOnly);
                if (solutionFiles.Length > 0)
                {
                    string solutionFile = solutionFiles[0];
                    Logger.LogDebug("Found solution file: {SolutionFile}", solutionFile);
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
                        Logger.LogDebug("Found project file in src: {ProjectFile}", projectFile);
                        return projectFile;
                    }
                }

                // If no solution file, look for any project files in the root directory
                string[] rootProjectFiles = Directory.GetFiles(sdkOutputDir, "*.csproj", SearchOption.TopDirectoryOnly);
                if (rootProjectFiles.Length > 0)
                {
                    string projectFile = rootProjectFiles[0];
                    Logger.LogDebug("Found project file in root: {ProjectFile}", projectFile); 
                    return projectFile;
                }

                throw new FileNotFoundException($"No solution (.sln) or project (.csproj) files found in: {sdkOutputDir}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error while determining build target in {sdkOutputDir}", ex);
            }
        }
    }
}
