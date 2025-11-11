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

    private static readonly Option<string?> ReleaseTypeOption = new("--release-type")
    {
        Description = "Specifies whether the next version is 'beta' or 'stable'.",
        Required = false
    };

    private static readonly Option<string?> VersionOption = new("--version")
    {
        Description = "Specifies the next version number.",
        Required = false
    };

    private static readonly Option<string?> ReleaseDateOption = new("--release-date")
    {
        Description = "The date (YYYY-MM-DD) to write into the changelog.",
        Required = false
    };

    protected override Command GetCommand() =>
        new(UpdateVersionCommandName, "Updates version and release date for Azure SDK packages.") 
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
    [McpServerTool(Name = "azsdk_package_update_version"), Description("Updates the version and release date for a specified package.")]
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

            if (!Directory.Exists(packagePath))
            {
                return PackageOperationResponse.CreateFailure($"Package path does not exist: {packagePath}");
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
                        { "PackagePath", packagePath }
                    };

                    // Add optional parameters if provided
                    if (!string.IsNullOrWhiteSpace(releaseType))
                    {
                        scriptParameters["ReleaseType"] = releaseType;
                    }

                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        scriptParameters["Version"] = version;
                    }

                    if (!string.IsNullOrWhiteSpace(releaseDate))
                    {
                        scriptParameters["ReleaseDate"] = releaseDate;
                    }
                    
                    // Create and execute process options for the update-version script
                    var processOptions = _specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters);
                    if (processOptions != null)
                    {
                        return await _specGenSdkConfigHelper.ExecuteProcessAsync(
                            processOptions,
                            ct,
                            packageInfo,
                            $"Version updated to {packageInfo?.PackageVersion}" + 
                                (!string.IsNullOrWhiteSpace(releaseDate) ? $" and release date set to {releaseDate}." : "."),
                            ["Review the updated version and release date", "Run validation checks"]);
                    }
                }
            }

            // Run default logic to update version
            logger.LogInformation("Running default logic to update version for the package...");            
            return await languageService.UpdateVersionAsync(packagePath, version, releaseDate, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while updating version for package: {PackagePath}", packagePath);
            return PackageOperationResponse.CreateFailure(
                $"An error occurred: {ex.Message}",
                nextSteps: [
                    "Check the running logs for details about the error",
                    "resolve the issue",
                    "re-run the tool",
                    "run verify setup tool if the issue is environment related"
                    ]);
        }
    }
}
