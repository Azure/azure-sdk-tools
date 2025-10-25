// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tools.Package
{
    /// <summary>
    /// Tool for updating changelog files in SDK packages.
    /// Handles automatic generation for management-plane packages and provides guidance for data-plane packages.
    /// </summary>
    [Description("Update changelog files for SDK packages")]
    [McpServerToolType]
    public class ChangelogUpdateTool : ConfigBasedTool
    {
        private readonly IGitHelper gitHelper;

        public ChangelogUpdateTool(
            ISpecGenSdkConfigHelper specGenSdkConfigHelper,
            ILogger<ChangelogUpdateTool> logger,
            IProcessHelper processHelper,
            IGitHelper gitHelper)
            : base(specGenSdkConfigHelper, logger, processHelper)
        {
            this.gitHelper = gitHelper;
        }

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

        private const string UpdateCommandName = "update-changelog";

        protected override Command GetCommand() =>
            new(UpdateCommandName, "Update changelog for an SDK package based on package path")
            {
                SharedOptions.PackagePath
            };

        public async override Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
            return await UpdateChangelogAsync(packagePath, ct);
        }

        /// <summary>
        /// Updates the changelog for the specified package.
        /// For management-plane packages: invokes changelog generation script.
        /// For data-plane packages: returns guidance for manual editing.
        /// </summary>
        /// <param name="packagePath">Absolute path to the SDK package directory.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the changelog update operation.</returns>
        [McpServerTool(Name = "azsdk_package_update_changelog_content"), Description("Update changelog content for an SDK package.")]
        public async Task<DefaultCommandResponse> UpdateChangelogAsync(
            [Description("Absolute path to the SDK package directory.")]
            string packagePath,
            CancellationToken ct = default)
        {
            try
            {
                logger.LogInformation($"Updating changelog for package at: {packagePath}");

                // Validate package path
                var validationError = ValidatePackagePath<DefaultCommandResponse>(packagePath);
                if (validationError != null) 
                {
                    return validationError;
                }

                // Get SDK repository root
                string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
                if (string.IsNullOrEmpty(sdkRepoRoot))
                {
                    return CreateFailureResponse<DefaultCommandResponse>($"Failed to discover local sdk repo with packagePath: {packagePath}.");
                }

                // Determine package type (mgmt vs data-plane)
                var packageType = DeterminePackageType(packagePath, sdkRepoRoot);
                var isManagementPlane = packageType.Equals("mgmt", StringComparison.OrdinalIgnoreCase);

                logger.LogInformation($"Package type detected: {packageType}");

                if (isManagementPlane)
                {
                    return await HandleManagementPlaneChangelogAsync(sdkRepoRoot, packagePath, ct);
                }
                else
                {
                    return HandleDataPlaneChangelog();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while updating changelog for package at: {PackagePath}", packagePath);
                return CreateFailureResponse<DefaultCommandResponse>($"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles changelog update for management-plane packages by invoking the update-changelog script.
        /// </summary>
        /// <param name="sdkRepoRoot">Root path of the SDK repository.</param>
        /// <param name="packagePath">Path to the package directory.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A response indicating the result of the script execution.</returns>
        private async Task<DefaultCommandResponse> HandleManagementPlaneChangelogAsync(
            string sdkRepoRoot,
            string packagePath,
            CancellationToken ct)
        {
            // Prepare script parameters
            var scriptParameters = new Dictionary<string, string>
            {
                { "SdkRepoPath", sdkRepoRoot },
                { "PackagePath", packagePath }
            };

            // Create and execute process options for the update-changelog script
            var processOptions = await CreateProcessOptions(ConfigType.UpdateChangelog, sdkRepoRoot, packagePath, scriptParameters, 15);
            return await ExecuteProcessAsync(processOptions, ct, "Changelog content is updated.", ["Update the version if it's a release."]);
        }

        /// <summary>
        /// Handles changelog guidance for data-plane packages.
        /// </summary>
        /// <returns>A noop response with guidance for manual editing.</returns>
        private DefaultCommandResponse HandleDataPlaneChangelog()
        {
            logger.LogInformation("Data-plane package detected, providing manual editing guidance");
            
            return new DefaultCommandResponse
            {
                Result = "noop",
                Message = "Data-plane changelog untouched; manual edits required.",
                NextSteps = ["Update the version if it's a release."]
            };
        }

        /// <summary>
        /// Determines the package type (mgmt vs data-plane) for the specified package.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <param name="sdkRepoRoot">The SDK repository root.</param>
        /// <returns>The package type string.</returns>
        private string DeterminePackageType(string packagePath, string sdkRepoRoot)
        {
            // Analyze path structure for common patterns
            var relativePath = Path.GetRelativePath(sdkRepoRoot, packagePath);

            // Common patterns for management-plane packages
            if (relativePath.Contains("arm-", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("mgmt-", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("resourcemanager", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Package type determined from path: mgmt");
                return "mgmt";
            }

            // Default to data-plane if no clear indicators
            logger.LogInformation("Package type defaulted to: data-plane");
            return "data-plane";
        }
    }
}
