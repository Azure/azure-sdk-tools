// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly ISpecGenSdkConfigHelper specGenSdkConfigHelper;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    /// <param name="specGenSdkConfigHelper">The configuration helper for validating compilation after updates.</param>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
        this.specGenSdkConfigHelper = specGenSdkConfigHelper;
    }
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec];

    private readonly Argument<string> updateCommitSha = new("update-commit-sha")
    {
        Description = "SHA of the commit to apply update changes for",
        Arity = ArgumentArity.ExactlyOne
    };

    private const string NO_CUSTOMIZATIONS_FOUND_NEXT_STEPS =
        "No customizations found. Code regeneration completed successfully.\n" +
        "Next steps:\n" +
        "1. Review generated code changes\n" +
        "2. Create customizations if needed\n" +
        "3. Open a pull request with your changes";

    private const string PATCHES_APPLIED_GUIDANCE = "Patches applied automatically and code regenerated with validation.\n" +
        "Next steps:\n" +
        "1. Review applied changes in customization files\n" +
        "2. Review generated code after customization updates to ensure it meets your code requirements\n" +
        "3. Fix any remaining issues if needed\n" +
        "4. Open a pull request with your changes";

    private const string PATCHES_FAILED_GUIDANCE = "Manual review required - automatic patches unsuccessful or not needed.\n" +
        "1. Compare generated code with your customizations\n" +
        "2. Update customization files manually\n" +
        "3. Regenerate with updated customization code to ensure it meets your code requirements\n" +
        "4. Open a pull request with your changes";
    protected override Command GetCommand() =>
       new("customized-update", "Update customized TypeSpec-generated client code with automated patch analysis.")
       {
            updateCommitSha, SharedOptions.PackagePath,
       };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var spec = parseResult.GetValue(updateCommitSha);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        try
        {
            logger.LogInformation("Starting client update for {packagePath}", packagePath);
            return await RunUpdateAsync(spec, packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Client update failed");
            return new CustomizedCodeUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" };
        }
    }

    [McpServerTool(Name = "azsdk_customized_code_update"), Description("Update customized TypeSpec-generated client code")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, ct);

    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string commitSha, string packagePath, CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new CustomizedCodeUpdateResponse { ErrorCode = "1", ResponseError = $"Package path does not exist: {packagePath}" };
            }
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new CustomizedCodeUpdateResponse { ErrorCode = "1", ResponseError = "Commit SHA is required." };
            }
            var languageService = GetLanguageService(packagePath);
            if (!languageService.IsCustomizedCodeUpdateSupported)
            {
                return new CustomizedCodeUpdateResponse { ErrorCode = "NoLanguageService", ResponseError = "Could not resolve a client update language service." };
            }

            return await UpdateCoreAsync(commitSha, packagePath, languageService, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed");
            return new CustomizedCodeUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<CustomizedCodeUpdateResponse> UpdateCoreAsync(string commitSha, string packagePath, LanguageService languageService, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("UpdateCoreAsync called with packagePath: {PackagePath}", packagePath ?? "(null)");
            var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
            logger.LogInformation("Regenerating code...");
            var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, commitSha, isCli: false, ct);
            if (!regenResult.IsSuccessful)
            {
                return new CustomizedCodeUpdateResponse
                {
                    ErrorCode = "RegenerateFailed",
                    ResponseError = regenResult.ResponseError
                };
            }

            var customizationRoot = languageService.GetCustomizationRoot(packagePath, ct);
            logger.LogDebug("Customization root: {CustomizationRoot}", customizationRoot ?? "(none)");

            // Use automated analysis and patch application for customization updates
            var (guidance, patchesApplied, requiresReview) = await GenerateGuidanceAndApplyPatchesAsync(commitSha, customizationRoot, packagePath, languageService, ct);

            // If patches were applied, regenerate the code to ensure customizations are properly integrated
            if (patchesApplied)
            {
                logger.LogInformation("Patches were applied. Regenerating code to validate customizations...");
                var regenAfterPatchResult = await RegenerateAfterPatchesAsync(tspLocationPath, packagePath, commitSha, ct);
                if (!regenAfterPatchResult.Success)
                {
                    logger.LogWarning("Code regeneration failed: {Error}", regenAfterPatchResult.ErrorMessage);
                    guidance.Insert(0, "Code regeneration after patches failed. Manual intervention required.");
                    guidance.Insert(1, $"Error: {regenAfterPatchResult.ErrorMessage}");
                    guidance.Insert(2, "");
                    requiresReview = true;
                }
                else
                {
                    logger.LogInformation("Regeneration successful, validating...");
                    logger.LogInformation("About to call ValidateAndUpdateGuidanceAsync with packagePath: {PackagePath}", packagePath ?? "(null)");
                    var (validationSuccess, validationRequiresReview) = await ValidateAndUpdateGuidanceAsync(packagePath, languageService, guidance, ct);
                    if (!validationSuccess)
                    {
                        requiresReview = true;
                    }
                    requiresReview = requiresReview || validationRequiresReview;
                }
            }

            return new CustomizedCodeUpdateResponse
            {
                NextSteps = guidance
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Core update failed");
            return new CustomizedCodeUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAfterPatchesAsync(string tspLocationPath, string packagePath, string commitSha, CancellationToken ct)
    {
        try
        {
            var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, commitSha, isCli: false, ct);

            if (!regenResult.IsSuccessful)
            {
                logger.LogError("Code regeneration failed: {Error}", regenResult.ResponseError);
                return (false, regenResult.ResponseError ?? "Code regeneration failed");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during code regeneration after patches");
            return (false, ex.Message);
        }
    }
    private async Task<(bool Success, bool RequiresReview)> ValidateAndUpdateGuidanceAsync(
        string packagePath,
        LanguageService languageService,
        List<string> guidance,
        CancellationToken ct)
    {
        logger.LogInformation("ValidateAndUpdateGuidanceAsync called with packagePath: {PackagePath}", packagePath ?? "(null)");
        
        // First try to build the code to ensure it compiles after patch application
        // This validates that the regenerated code with applied patches builds successfully
        var buildResult = await BuildSdkAsync(packagePath, languageService, ct);
        
        if (buildResult.ExitCode == 0)
        {
            logger.LogInformation("Build completed successfully");
            
            // If build succeeds, also run language-specific validation if available
            var validationResult = await languageService.ValidateAsync(packagePath, ct);
            
            if (validationResult.Success)
            {
                logger.LogInformation("Build and validation passed");
                guidance.Insert(0, "Code regenerated, built, and validated successfully after applying patches.");
                guidance.Insert(1, "");
                return (true, false);
            }
            else
            {
                logger.LogWarning("Build succeeded but validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                guidance.Insert(0, "Code regenerated and built successfully after applying patches, but validation failed.");
                guidance.Insert(1, $"Validation warnings: {string.Join(", ", validationResult.Errors)}");
                guidance.Insert(2, "");
                return (true, true); // Build succeeded but validation had issues
            }
        }
        else
        {
            logger.LogError("Build failed: {Error}", buildResult.ResponseError);
            guidance.Insert(0, "Code regenerated but build failed after applying patches.");
            guidance.Insert(1, $"Build errors: {buildResult.ResponseError}");
            guidance.Insert(2, "");
            return (false, true);
        }
    }

    /// <summary>
    /// Builds the SDK project to validate compilation.
    /// </summary>
    private async Task<PackageOperationResponse> BuildSdkAsync(string packagePath, LanguageService languageService, CancellationToken ct)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(packagePath))
            {
                logger.LogError("Package path is null or empty in BuildSdkAsync");
                return PackageOperationResponse.CreateFailure("Package path is required.");
            }

            if (!Directory.Exists(packagePath))
            {
                logger.LogError("Package path does not exist: {PackagePath}", packagePath);
                return PackageOperationResponse.CreateFailure($"Path does not exist: {packagePath}");
            }

            logger.LogInformation("Building SDK for project path: {PackagePath}", packagePath);

            // Get repository root path from project path
            string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return PackageOperationResponse.CreateFailure($"Failed to discover local sdk repo with project-path: {packagePath}.");
            }

            string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
            PackageInfo? packageInfo = await languageService.GetPackageInfo(packagePath, ct);
            
            // Return if the project is python project (Python SDKs don't require compilation)
            if (sdkRepoName.Contains("azure-sdk-for-python", StringComparison.OrdinalIgnoreCase))
            {
                return PackageOperationResponse.CreateSuccess("Python SDK project detected. Skipping build step as Python SDKs do not require a build process.", packageInfo, result: "noop");
            }

            // Get build configuration and execute if found
            var (configContentType, configValue) = await specGenSdkConfigHelper.GetConfigurationAsync(sdkRepoRoot, SpecGenSdkConfigType.Build);
            if (configContentType != SpecGenSdkConfigContentType.Unknown && !string.IsNullOrEmpty(configValue))
            {
                // Prepare script parameters
                var scriptParameters = new Dictionary<string, string>
                {
                    { "PackagePath", packagePath }
                };
                
                // Create and execute process options for the build script
                var processOptions = specGenSdkConfigHelper.CreateProcessOptions(configContentType, configValue, sdkRepoRoot, packagePath, scriptParameters, 30);
                if (processOptions != null)
                {
                    return await specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Build completed successfully.");
                }
            }
            
            return PackageOperationResponse.CreateFailure("No build configuration found or failed to prepare the build command", packageInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while building SDK");
            return PackageOperationResponse.CreateFailure($"An error occurred: {ex.Message}");
        }
    }

    private async Task<(List<string> guidance, bool patchesApplied, bool requiresReview)> GenerateGuidanceAndApplyPatchesAsync(
        string commitSha,
        string? customizationRoot,
        string packagePath,
        LanguageService languageService,
        CancellationToken ct)
    {
        try
        {
            logger.LogInformation("GenerateGuidanceAndApplyPatchesAsync called with packagePath: {PackagePath}", packagePath ?? "(null)");
            if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
            {
                logger.LogInformation("No customizations found to patch");

                var basicGuidance = new List<string>
                {
                    NO_CUSTOMIZATIONS_FOUND_NEXT_STEPS
                };
                return (basicGuidance, false, false);
            }

            logger.LogInformation("Applying patches...");
            var patchesApplied = await languageService.ApplyPatchesAsync(commitSha, customizationRoot, packagePath, ct);
            logger.LogDebug("Patch application result: {Success}", patchesApplied);

            var guidance = new List<string>();
            bool requiresReview = true; // Always require review after automatic changes

            if (patchesApplied)
            {

                guidance.Add(PATCHES_APPLIED_GUIDANCE);
            }
            else
            {
                guidance.Add(PATCHES_FAILED_GUIDANCE);
            }

            return (guidance, patchesApplied, requiresReview);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate guidance and apply patches");
            var errorGuidance = new List<string>
            {
                "Automatic patch application failed. Manual review required.",
                $"Error: {ex.Message}",
                "1. Review generated code changes",
                "2. Update customization files manually",
                "3. Open a pull request with your changes"
            };
            return (errorGuidance, false, true);
        }
    }
}
