using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to build/compile SDK code locally.")]
    public class SdkBuildTool : LanguageMcpTool
    {
        // Fields to hold constructor parameters
        private readonly IProcessHelper processHelper;
        private readonly ISpecGenSdkConfigHelper specGenSdkConfigHelper;

        public SdkBuildTool(
            IGitHelper gitHelper,
            ILogger<SdkBuildTool> logger,
            IProcessHelper processHelper,
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            IEnumerable<LanguageService> languageServices
        ) : base(languageServices, gitHelper, logger)
        {
            this.processHelper = processHelper;
            this.specGenSdkConfigHelper = specGenSdkConfigHelper;
        }

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
        public async Task<PackageOperationResponse> BuildSdkAsync(
            [Description("Absolute path to the SDK project.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation("Building SDK for project path: {PackagePath}", packagePath);

                // Validate inputs
                if (string.IsNullOrEmpty(packagePath))
                {
                    return PackageOperationResponse.CreateFailure("Package path is required.");
                }

                if (!Directory.Exists(packagePath))
                {
                    return PackageOperationResponse.CreateFailure($"Path does not exist: {packagePath}");
                }

                // Get repository root path from project path
                string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return PackageOperationResponse.CreateFailure($"Failed to discover local sdk repo with project-path: {packagePath}.");
                }

                logger.LogInformation("Repository root path: {SdkRepoRoot}", sdkRepoRoot);
                string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
                logger.LogInformation("Repository name: {SdkRepoName}", sdkRepoName);

                PackageInfo? packageInfo = await GetPackageInfo(packagePath, ct);
                // Return if the project is python project
                if (sdkRepoName.Contains(AzureSdkForPythonRepoName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return PackageOperationResponse.CreateSuccess("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.", packageInfo);
                }

                // Get the build configuration (command or script path)
                ProcessOptions options;
                try
                {
                    options = await CreateProcessOptions(sdkRepoRoot, packagePath);
                }
                catch (Exception ex)
                {
                    return PackageOperationResponse.CreateFailure($"Failed to get build configuration: {ex.Message}", packageInfo);
                }

                // Run the build script or command
                logger.LogInformation("Executing build process...");
                var buildResult = await processHelper.Run(options, ct);
                var trimmedBuildResult = (buildResult.Output ?? string.Empty).Trim();
                if (buildResult.ExitCode != 0)
                {
                    return PackageOperationResponse.CreateFailure($"Build process failed with exit code {buildResult.ExitCode}. Output:\n{trimmedBuildResult}", packageInfo);
                }

                logger.LogInformation("Build process execution completed");
                return PackageOperationResponse.CreateSuccess($"Build completed successfully. Output:\n{trimmedBuildResult}", packageInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}");
            }
        }

        private async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct)
        {
            PackageInfo? packageInfo = null;
            try
            {
                var languageService = GetLanguageService(packagePath);
                if (languageService != null)
                {
                    packageInfo = await languageService.GetPackageInfo(packagePath, ct);
                }
                else
                {
                    logger.LogError("No package info helper found for package path: {packagePath}", packagePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while parsing package path: {packagePath}", packagePath);
            }
            return packageInfo;
        }

        // Helper method to create failure responses along with setting the failure state
        private PackageOperationResponse CreateFailureResponse(string message, PackageInfo? packageInfo = null)
        {
            return new PackageOperationResponse
            {
                ResponseErrors = [message],
                PackageName = packageInfo?.PackageName ?? string.Empty,
                Language = packageInfo?.Language ?? SdkLanguage.Unknown,
                PackageType = packageInfo?.SdkType ?? SdkType.Unknown
            };
        }

        // Helper method to create success responses (no SetFailure needed)
        private PackageOperationResponse CreateSuccessResponse(string message, PackageInfo? packageInfo)
        {
            return new PackageOperationResponse
            {
                Result = "succeeded",
                Message = message,
                PackageName = packageInfo?.PackageName ?? string.Empty,
                Language = packageInfo?.Language ?? SdkLanguage.Unknown,
                PackageType = packageInfo?.SdkType ?? SdkType.Unknown
            };
        }

        // Create process options for building the SDK based on configuration
        private async Task<ProcessOptions> CreateProcessOptions(string sdkRepoRoot, string packagePath)
        {
            var (configType, configValue) = await specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.Build);

            if (configType == SpecGenSdkConfigContentType.Command)
            {
                // Execute as command
                var variables = new Dictionary<string, string>
                {
                    { "packagePath", packagePath }
                };

                var substitutedCommand = specGenSdkConfigHelper.SubstituteCommandVariables(configValue, variables);
                logger.LogInformation("Executing build command: {SubstitutedCommand}", substitutedCommand);

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
            else // BuildConfigType.ScriptPath
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

                logger.LogInformation("Executing build script file: {BuildScriptPath}", fullBuildScriptPath);

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
