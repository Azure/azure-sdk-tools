using System.CommandLine;
using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    [McpServerToolType, Description("This type contains the tools to build/compile SDK code locally.")]
    public class SdkBuildTool : LanguageMcpTool
    {
        private readonly IRawOutputHelper _outputHelper;

        public SdkBuildTool(
            IGitHelper gitHelper,
            ILogger<SdkBuildTool> logger,
            IEnumerable<LanguageService> languageServices,
            IRawOutputHelper outputHelper
        ) : base(languageServices, gitHelper, logger)
        {
            _outputHelper = outputHelper;
        }

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        // Command names
        private const string BuildSdkCommandName = "build";
        private const string BuildSdkToolName = "azsdk_package_build_code";
        private const int CommandTimeoutInMinutes = 30;

        protected override Command GetCommand() =>
            new McpCommand(BuildSdkCommandName, "Builds SDK source code for a specified language and project", BuildSdkToolName) { SharedOptions.PackagePath };

        public async override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            return await BuildSdkAsync(null, packagePath, ct);
        }

        [McpServerTool(Name = BuildSdkToolName), Description("Build/compile SDK code for a specified project locally.")]
        public async Task<PackageOperationResponse> BuildSdkAsync(
            IProgress<ProgressNotificationValue>? progress,
            [Description("Absolute path to the SDK project.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                var reporter = new ProgressReporter(progress, logger, totalSteps: 4, _outputHelper);

                reporter.NextStep("Validating package path");

                // Validate package path
                if (string.IsNullOrWhiteSpace(packagePath))
                {
                    return PackageOperationResponse.CreateFailure("Package path is required and cannot be empty.");
                }

                // Resolves relative paths to absolute
                string fullPath = Path.GetFullPath(packagePath);

                reporter.NextStep("Detecting SDK language");
                var languageService = await GetLanguageServiceAsync(fullPath, ct);
                if (languageService == null)
                {
                    return PackageOperationResponse.CreateFailure($"Failed to find the language from package path {packagePath}");
                }

                // Use the shared BuildAsync method from LanguageService
                reporter.NextStep($"Starting {languageService.Language} SDK build");
                logger.LogInformation("Building SDK project at: {PackagePath} for language: {Language}", fullPath, languageService.Language);
                bool success;
                string? errorMessage;
                PackageInfo? packageInfo;
                await using (reporter.StartHeartbeat($"Building {languageService.Language} SDK project", ct))
                {
                    (success, errorMessage, packageInfo) = await languageService.BuildAsync(fullPath, CommandTimeoutInMinutes, ct);
                }

                reporter.NextStep(success ? "Build completed successfully" : "Build failed");

                if (success)
                {
                    // Check if this was a Python no-op build
                    if (languageService.Language == SdkLanguage.Python)
                    {
                        return PackageOperationResponse.CreateSuccess(
                            "Python SDK project detected. Skipping build step as Python SDKs do not require a build process.",
                            packageInfo,
                            result: "noop");
                    }
                    return PackageOperationResponse.CreateSuccess("Build completed successfully.", packageInfo);
                }

                return PackageOperationResponse.CreateFailure(
                    errorMessage ?? "Build failed with an unknown error.",
                    packageInfo,
                    nextSteps: [
                        "Ensure the SDK repository has a valid 'buildScript' configuration in eng/swagger_to_sdk_config.json",
                        "Check the build logs for details about the error",
                        "Resolve any issues reported in the build log",
                        "Re-run the tool",
                        "Run verify setup tool if the issue is environment related"
                    ]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while building SDK");
                return PackageOperationResponse.CreateFailure(
                    $"An error occurred: {ex.Message}",
                    nextSteps: [
                        "Check the build logs for details about the error",
                        "Resolve the issue",
                        "Re-run the tool",
                        "Run verify setup tool if the issue is environment related"
                    ]);
            }
        }
    }
}
