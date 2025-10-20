using System.CommandLine;
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

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        // Command names
        private const string BuildSdkCommandName = "build";
        private const string AzureSdkForPythonRepoName = "azure-sdk-for-python";
        private const int CommandTimeoutInMinutes = 30;

        protected override Command GetCommand() =>
            new(BuildSdkCommandName, "Builds SDK source code for a specified language and project") { SharedOptions.PackagePath };

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
                var languageService = GetLanguageService(sdkRepoRoot);
                if (languageService == null)
                {
                    return PackageOperationResponse.CreateFailure($"Failed to find the language from package path {packagePath}");
                }
                PackageInfo? packageInfo = await languageService.GetPackageInfo(packagePath, ct);
                // Return if the project is python project
                if (sdkRepoName.Contains(AzureSdkForPythonRepoName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.");
                    return PackageOperationResponse.CreateSuccess("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.", packageInfo, result: "noop");
                }

                var (configContentType, configValue) = await this.specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.Build);
                if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                {
                    logger.LogInformation("Found valid configuration for build process. Executing configured script...");

                    // Prepare script parameters
                    var scriptParameters = new Dictionary<string, string>
                    {
                        { "PackagePath", packagePath }
                    };
                    
                    // Create and execute process options for the build script
                    var processOptions = this.specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters, CommandTimeoutInMinutes);
                    if (processOptions != null)
                    {
                        return await this.specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Build completed successfully.");
                    }
                }
                return PackageOperationResponse.CreateFailure("No build configuration found or failed to prepare the build command", packageInfo, nextSteps: ["Ensure the SDK repository has a valid 'buildScript' configuration in eng/swagger_to_sdk_config.json", "Resolve any issues reported in the build log", "Re-run the tool"]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}", nextSteps: ["Check the build logs for details about the error", "Resolve the issue", "Re-run the tool"]);
            }
        }
    }
}
