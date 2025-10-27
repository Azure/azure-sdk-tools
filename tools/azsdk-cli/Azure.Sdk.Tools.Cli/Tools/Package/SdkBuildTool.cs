using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to build/compile SDK code locally.")]
    public class SdkBuildTool(
        IGitHelper gitHelper,
        ILogger<SdkBuildTool> logger,
        IProcessHelper processHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper
    ) : MCPTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package, SharedCommandGroups.SourceCode];

        // Command names
        private const string BuildSdkCommandName = "build";
        private const string AzureSdkForPythonRepoName = "azure-sdk-for-python";
        private const int CommandTimeoutInMinutes = 30;

        protected override Command GetCommand() =>
            new(BuildSdkCommandName, "Builds SDK source code for a specified language and project.") { SharedOptions.PackagePath };

        public async override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            return await BuildSdkAsync(packagePath, ct);
        }

        [McpServerTool(Name = "azsdk_package_build_code"), Description("Build/compile SDK code for a specified project locally.")]
        public async Task<DefaultCommandResponse> BuildSdkAsync(
            [Description("Absolute path to the SDK project.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Building SDK for project path: {packagePath}");

                // Validate inputs
                if (string.IsNullOrEmpty(packagePath))
                {
                    return CreateFailureResponse("Package path is required.");
                }

                if (!Directory.Exists(packagePath))
                {
                    return CreateFailureResponse($"Path does not exist: {packagePath}");
                }

                // Get repository root path from project path
                string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return CreateFailureResponse($"Failed to discover local sdk repo with project-path: {packagePath}.");
                }

                logger.LogInformation($"Repository root path: {sdkRepoRoot}");

                string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
                logger.LogInformation($"Repository name: {sdkRepoName}");

                // Return if the project is python project
                if (sdkRepoName.Contains(AzureSdkForPythonRepoName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return CreateSuccessResponse("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                }

                // Get the build configuration (command or script path)
                ProcessOptions options;
                try
                {
                    options = await CreateProcessOptions(sdkRepoRoot, packagePath);
                }
                catch (Exception ex)
                {
                    return CreateFailureResponse($"Failed to get build configuration: {ex.Message}");
                }

                // Run the build script or command
                logger.LogInformation($"Executing build process...");
                var buildResult = await processHelper.Run(options, ct);
                var trimmedBuildResult = (buildResult.Output ?? string.Empty).Trim();
                if (buildResult.ExitCode != 0)
                {
                    return CreateFailureResponse($"Build process failed with exit code {buildResult.ExitCode}. Output:\n{trimmedBuildResult}");
                }

                logger.LogInformation("Build process execution completed");
                return CreateSuccessResponse($"Build completed successfully. Output:\n{trimmedBuildResult}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return CreateFailureResponse($"An error occurred: {ex.Message}");
            }
        }

        // Helper method to create failure responses along with setting the failure state
        private DefaultCommandResponse CreateFailureResponse(string message)
        {
            return new DefaultCommandResponse
            {
                ResponseErrors = [message]
            };
        }

        // Helper method to create success responses (no SetFailure needed)
        private DefaultCommandResponse CreateSuccessResponse(string message)
        {
            return new DefaultCommandResponse
            {
                Result = "succeeded",
                Message = message
            };
        }

        // Create process options for building the SDK based on configuration
        private async Task<ProcessOptions> CreateProcessOptions(string sdkRepoRoot, string packagePath)
        {
            var (configType, configValue) = await specGenSdkConfigHelper.GetBuildConfigurationAsync(sdkRepoRoot);

            if (configType == ConfigContentType.Command)
            {
                // Execute as command
                var variables = new Dictionary<string, string>
                {
                    { "packagePath", packagePath }
                };

                var substitutedCommand = specGenSdkConfigHelper.SubstituteCommandVariables(configValue, variables);
                logger.LogInformation($"Executing build command: {substitutedCommand}");

                var commandParts = specGenSdkConfigHelper.ParseCommand(substitutedCommand);
                if (commandParts.Length == 0)
                {
                    throw new InvalidOperationException($"Invalid build command: {substitutedCommand}");
                }

                return new ProcessOptions(
                    commandParts[0],
                    commandParts.Skip(1).ToArray(),
                    logOutputStream: true,
                    workingDirectory: packagePath,
                    timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
                );
            }
            else // ConfigContentType.ScriptPath
            {
                // Execute as script file
                // Always resolve relative paths against sdkRepoRoot, then normalize
                var fullBuildScriptPath = Path.IsPathRooted(configValue)
                    ? configValue
                    : Path.Combine(sdkRepoRoot, configValue);

                // Normalize the final path
                fullBuildScriptPath = Path.GetFullPath(fullBuildScriptPath);

                if (!File.Exists(fullBuildScriptPath))
                {
                    throw new FileNotFoundException($"Build script not found at: {fullBuildScriptPath}");
                }

                logger.LogInformation($"Executing build script file: {fullBuildScriptPath}");

                return new PowershellOptions(
                    fullBuildScriptPath,
                    ["-PackagePath", packagePath],
                    logOutputStream: true,
                    workingDirectory: sdkRepoRoot,
                    timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
                );
            }
        }
    }
}
