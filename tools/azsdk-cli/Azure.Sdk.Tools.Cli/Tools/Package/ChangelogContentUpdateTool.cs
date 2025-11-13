using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Package;

/// <summary>
/// Tool for updating changelog content in Azure SDK packages.
/// Supports running configured script or built-in code for update flows.
/// </summary>
[McpServerToolType, Description("This type contains the tools to update changelog content for Azure SDK packages.")]
public class ChangelogContentUpdateTool : LanguageMcpTool
{
    private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;

    public ChangelogContentUpdateTool(
        IGitHelper gitHelper,
        ILogger<ChangelogContentUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper): base(languageServices, gitHelper, logger)
    {
        _specGenSdkConfigHelper = specGenSdkConfigHelper;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private const string UpdateChangelogContentCommandName = "update-changelog-content";

    protected override Command GetCommand() =>
        new(UpdateChangelogContentCommandName, "Updates changelog content for Azure SDK packages.") { SharedOptions.PackagePath };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        return await UpdateChangelogContentAsync(packagePath, ct);
    }

    /// <summary>
    /// Updates the changelog content for a specified package.
    /// For management-plane packages: invokes changelog update script.
    /// For data-plane packages: returns guidance for manual editing.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the changelog update operation.</returns>
    [McpServerTool(Name = "azsdk_package_update_changelog_content"), Description("Updates the changelog content for a specified package.")]
    public async Task<PackageOperationResponse> UpdateChangelogContentAsync(
        [Description("The absolute path to the package directory.")] string packagePath,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Updating changelog content for package at: {packagePath}", packagePath);

            // Validate package path
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return PackageOperationResponse.CreateFailure("Package path is required and cannot be empty.");
            }

            if (!Directory.Exists(packagePath))
            {
                return PackageOperationResponse.CreateFailure($"Package path does not exist: {packagePath}");
            }

            // Discover the repository root
            var sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
            if (sdkRepoRoot == null)
            {
                return PackageOperationResponse.CreateFailure("Unable to find git repository root from the provided package path.");
            }

            logger.LogInformation("Repository root discovered: {SdkRepoRoot}", sdkRepoRoot);
            var languageService = GetLanguageService(packagePath);
            if (languageService == null)
            {
                return PackageOperationResponse.CreateFailure("Tooling error: unable to determine language service for the specified package path.", nextSteps: ["Create an issue at the https://github.com/Azure/azure-sdk-tools/issues/new", "contact the Azure SDK team for assistance."]);
            }

            // Check for package type
            var packageInfo = await languageService.GetPackageInfo(packagePath, ct);
            packageInfo.SdkType = SdkType.Management; // Forcing management type for testing purposes
            if (packageInfo?.SdkType == SdkType.Management)
            {
                // For management-plane packages, execute configured changelog update script
                var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.UpdateChangelogContent);
                if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                {
                    logger.LogInformation("Found valid configuration for updating changelog content. Executing configured script...");

                    // Prepare script parameters
                    var scriptParameters = new Dictionary<string, string>
                    {
                        { "SdkRepoPath", sdkRepoRoot },
                        { "PackagePath", packagePath }
                    };
                    
                    // Create and execute process options for the update-changelog-content script
                    var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                    if (processOptions != null)
                    {
                        return await _specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Changelog content is updated.", ["Review the changelog for accuracy and completeness", "Update metadata for the package"]);
                    }
                }
            }

            // Run default logic to update changelog content
            logger.LogInformation("Running default logic to update changelog content for the package...");
            return await languageService.UpdateChangelogContentAsync(packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating changelog content for package: {PackagePath}", packagePath);
            return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}");
        }
    }
}
