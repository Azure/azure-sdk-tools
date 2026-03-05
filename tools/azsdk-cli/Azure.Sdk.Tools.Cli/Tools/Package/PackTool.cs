// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
    [McpServerToolType, Description("Create distributable artifacts for SDK packages")]
    public class PackTool : LanguageMcpTool
    {
        public PackTool(
            IGitHelper gitHelper,
            ILogger<PackTool> logger,
            IEnumerable<LanguageService> languageServices
        ) : base(languageServices, gitHelper, logger)
        {
        }

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string PackCommandName = "pack";
        private const string PackToolName = "azsdk_package_pack";
        private const int CommandTimeoutInMinutes = 30;

        private static readonly Option<string?> OutputPath = new("--output-path")
        {
            Description = "Output directory for generated artifacts. If not specified, uses the default location for the language.",
            Required = false,
        };

        protected override Command GetCommand() =>
            new McpCommand(PackCommandName, "Create distributable artifacts (e.g. .nupkg, .jar, .tgz, .whl) for an SDK package", PackToolName)
            {
                SharedOptions.PackagePath,
                OutputPath,
            };

        public async override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            var outputPath = parseResult.GetValue(OutputPath);
            return await PackAsync(null, packagePath, outputPath, ct);
        }

        [McpServerTool(Name = PackToolName), Description("Create distributable artifacts for the specified SDK package.")]
        public async Task<PackageOperationResponse> PackAsync(
            IProgress<ProgressNotificationValue>? progress,
            [Description("Absolute path to the SDK package directory.")]
            string packagePath,
            [Description("Optional output directory for the generated artifact.")]
            string? outputPath = null,
            CancellationToken ct = default)
        {
            try
            {
                var reporter = new ProgressReporter(progress, logger, totalSteps: 4);

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

                reporter.NextStep($"Packing {languageService.Language} SDK project");
                logger.LogInformation("Packing SDK project at: {PackagePath} for language: {Language}", fullPath, languageService.Language);

                bool success;
                string? errorMessage;
                PackageInfo? packageInfo;
                string? artifactPath;
                await using (reporter.StartHeartbeat($"Packing {languageService.Language} SDK project", ct))
                {
                    (success, errorMessage, packageInfo, artifactPath) = await languageService.PackAsync(fullPath, outputPath, CommandTimeoutInMinutes, ct);
                }

                reporter.NextStep(success ? "Pack completed successfully" : "Pack failed");

                if (success)
                {
                    return PackageOperationResponse.CreateSuccess(
                        artifactPath != null
                            ? $"Pack completed successfully. Artifact: {artifactPath}"
                            : errorMessage ?? "Pack completed successfully.",
                        packageInfo);
                }

                return PackageOperationResponse.CreateFailure(
                    errorMessage ?? "Pack failed with an unknown error.",
                    packageInfo,
                    nextSteps: [
                        "Ensure the package builds successfully before packing (run 'azsdk pkg build' or 'azsdk_package_build_code' tool first)",
                        "Check the pack logs for details about the error",
                        "Resolve any issues reported in the log",
                        "Re-run the tool",
                        "Run verify setup tool if the issue is environment related"
                    ]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while packing SDK");
                return PackageOperationResponse.CreateFailure(
                    $"An error occurred: {ex.Message}",
                    nextSteps: [
                        "Check the pack logs for details about the error",
                        "Resolve the issue",
                        "Re-run the tool",
                        "Run verify setup tool if the issue is environment related"
                    ]);
            }
        }
    }
}
