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
/// Tool for updating version in Azure SDK packages.
/// Supports running configured script or built-in code for update flows.
/// </summary>
[McpServerToolType, Description("This type contains the tools to update version and release date for Azure SDK packages.")]
public class VersionUpdateTool : LanguageMcpTool
{
    private readonly ISpecGenSdkConfigHelper _specGenSdkConfigHelper;

    public VersionUpdateTool(
        IGitHelper gitHelper,
        ILogger<VersionUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper): base(languageServices, gitHelper, logger)
    {
        _specGenSdkConfigHelper = specGenSdkConfigHelper;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.Package];

    private const string UpdateVersionCommandName = "update-version";
    private const string UpdateVersionToolName = "azsdk_package_update_version";

    private static readonly Option<string?> ReleaseTypeOption = new("--release-type", "-t")
    {
        Description = "Specifies whether the next version is 'beta' or 'stable'",
        Required = false
    };

    private static readonly Option<string?> VersionOption = new("--version", "-v")
    {
        Description = "Specifies the next version number",
        Required = false
    };

    private static readonly Option<string?> ReleaseDateOption = new("--release-date", "-d")
    {
        Description = "The date (YYYY-MM-DD) to write into the changelog",
        Required = false
    };

    protected override Command GetCommand() =>
        new McpCommand(UpdateVersionCommandName, "Updates version and release date for Azure SDK packages", UpdateVersionToolName) 
        { 
            SharedOptions.PackagePath,
            ReleaseTypeOption,
            VersionOption,
            ReleaseDateOption
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        var releaseType = parseResult.GetValue(ReleaseTypeOption);
        var version = parseResult.GetValue(VersionOption);
        var releaseDate = parseResult.GetValue(ReleaseDateOption);
        return await UpdateVersionAsync(packagePath, releaseType, version, releaseDate, ct);
    }

    /// <summary>
    /// Updates the version for a specified package.
    /// For management-plane packages: invokes version update script.
    /// For data-plane packages: returns guidance for manual editing.
    /// </summary>
    /// <param name="packagePath">The absolute path to the package directory.</param>
    /// <param name="releaseType">Specifies whether the next version is 'beta' or 'stable'.</param>
    /// <param name="version">Specifies the next version number.</param>
    /// <param name="releaseDate">The date (YYYY-MM-DD) to write into the changelog.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A response indicating the result of the version update operation.</returns>
    [McpServerTool(Name = UpdateVersionToolName), Description("Updates the version and release date for a specified package.")]
    public async Task<PackageOperationResponse> UpdateVersionAsync(
        [Description("The absolute path to the package directory.")] string packagePath,
        [Description("Specifies whether the next version is 'beta' or 'stable'.")] string? releaseType,
        [Description("Specifies the next version number.")] string? version,
        [Description("The date (YYYY-MM-DD) to write into the changelog.")] string? releaseDate,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Updating version for package with parameters: PackagePath: {packagePath}, ReleaseType: {ReleaseType}, Version: {Version}, ReleaseDate: {ReleaseDate}", 
                packagePath, releaseType ?? "not specified", version ?? "not specified", releaseDate ?? "not specified");

            // Validate package path
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                return PackageOperationResponse.CreateFailure("Package path is required and cannot be empty.");
            }

            // Resolves relative paths to absolute
            string fullPath = Path.GetFullPath(packagePath);
            
            if (!Directory.Exists(fullPath))
            {
                return PackageOperationResponse.CreateFailure($"Package full path does not exist: {fullPath}, input package path: {packagePath}.");
            }

            packagePath = fullPath;
            logger.LogInformation("Resolved package path: {PackagePath}", packagePath);

            // Validate and set release date
            if (string.IsNullOrWhiteSpace(releaseDate))
            {
                releaseDate = DateTime.Now.ToString("yyyy-MM-dd");
                logger.LogInformation("No release date specified. Setting to current date: {ReleaseDate}", releaseDate);
            }
            else
            {
                // Validate date format (YYYY-MM-DD)
                if (!DateTime.TryParseExact(releaseDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
                {
                    return PackageOperationResponse.CreateFailure(
                        $"Invalid release date format: {releaseDate}. Expected format: YYYY-MM-DD (e.g., 2025-11-12)",
                        nextSteps: [
                            "Provide the release date in the correct format: YYYY-MM-DD",
                            "Re-run the tool with the corrected release date"
                        ]);
                }
            }

            // Set default release type to 'beta' if both Version and ReleaseType are empty or null
            if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(releaseType))
            {
                releaseType = "beta";
                logger.LogInformation("No version or release type specified. Defaulting to release type: beta");
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
            if (packageInfo?.SdkType == SdkType.Management)
            {
                // For management-plane packages, execute configured version update script
                var (configContentType, configValue) = await _specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.UpdateVersion);
                if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
                {
                    logger.LogInformation("Found valid configuration for updating version. Executing configured script...");

                    // Prepare script parameters
                    var scriptParameters = new Dictionary<string, string>
                    {
                        { "SdkRepoPath", sdkRepoRoot },
                        { "PackagePath", packagePath },
                        { "ReleaseType", releaseType ?? string.Empty },
                        { "Version", version ?? string.Empty },
                        { "ReleaseDate", releaseDate }
                    };
                    
                    // Create and execute process options for the update-version script
                    var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                    if (processOptions != null)
                    {
                        // Use custom ExecuteProcessAsync that refetches packageInfo after the version update
                        return await ExecuteProcessAndRefetchPackageInfoAsync(
                            packagePath,
                            languageService,
                            processOptions,
                            packageInfo,
                            ct,
                            $"Version updated" + 
                                (!string.IsNullOrWhiteSpace(releaseDate) ? $" and release date set to {releaseDate}." : "."),
                            ["Review the updated version and release date", "Run validation checks"]);
                    }
                }
            }

            // Run default logic to update version
            logger.LogInformation("Running default logic to update version for the package...");
            return await languageService.UpdateVersionAsync(packagePath, releaseType, version, releaseDate, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating version for package: {PackagePath}", packagePath);
            return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a process and refetches package info after completion since version update changes the package metadata.
    /// </summary>
    private async Task<PackageOperationResponse> ExecuteProcessAndRefetchPackageInfoAsync(
        string packagePath,
        LanguageService languageService,
        ProcessOptions processOptions,
        PackageInfo? packageInfo,
        CancellationToken ct,
        string successMessage,
        string[]? nextSteps = null)
    {
        var result = await _specGenSdkConfigHelper.ExecuteProcessAsync(
            processOptions,
            ct,
            packageInfo,
            successMessage,
            nextSteps);

        // If the process succeeded, refetch the package info to get updated version
        if (result.OperationStatus == Status.Succeeded)
        {
            try
            {
                logger.LogInformation("Refetching package info after version update for: {PackagePath}", packagePath);
                var updatedPackageInfo = await languageService.GetPackageInfo(packagePath, ct);
                
                // Create a new response with the updated package info
                return PackageOperationResponse.CreateSuccess(
                    successMessage,
                    updatedPackageInfo,
                    nextSteps?.ToArray());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refetch package info after version update, returning result without updated info");
                // Return the original result if refetch fails
                return result;
            }
        }

        // If the process failed, return the result as-is
        return result;
    }
}
