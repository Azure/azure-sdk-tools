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

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

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
    /// <param name="specGenSdkConfigHelper">The configuration helper for building and validating code after updates.</param>
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

    // MCP Tool Names
    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

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

    private const string PATCHES_APPLIED_GUIDANCE = "Patches applied automatically and code regenerated with build validation.\n" +
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
       new McpCommand("customized-update", "Update customized TypeSpec-generated client code with automated patch analysis.", CustomizedCodeUpdateToolName)
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

    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Update customized TypeSpec-generated client code")]
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

            var guidance = new List<string>();

            // Check if customizations exist - only activate Phase B if customization files are present
            if (string.IsNullOrEmpty(customizationRoot))
            {
                logger.LogInformation("No customization files detected - Phase B skipped");
                guidance.Add(NO_CUSTOMIZATIONS_FOUND_NEXT_STEPS);
            }
            else
            {
                logger.LogInformation("Customization files detected at: {CustomizationRoot} - activating Phase B", customizationRoot);
                
                // Phase B: Apply patches to customization code
                var patchesApplied = await ApplyPatchesAsync(commitSha, customizationRoot, packagePath, languageService, ct);

                // If patches were applied, regenerate and build to validate the complete process
                if (patchesApplied)
                {
                    logger.LogInformation("Patches were applied. Regenerating code to validate customizations...");
                    var (Success, ErrorMessage) = await RegenerateAfterPatchesAsync(tspLocationPath, packagePath, commitSha, ct);
                    if (!Success)
                    {
                        logger.LogWarning("Code regeneration failed: {Error}", ErrorMessage);
                        guidance.Add($"Code regeneration failed after applying patches: {ErrorMessage}");
                        guidance.Add("");
                        guidance.Add(PATCHES_FAILED_GUIDANCE);
                    }
                    else
                    {
                        logger.LogInformation("Regeneration successful, building SDK code to validate...");
                        await BuildAndGenerateGuidanceAsync(packagePath, languageService, guidance, ct);
                    }
                }
                else
                {
                    // Customizations exist but patches were not applied or failed
                    guidance.Add(PATCHES_FAILED_GUIDANCE);
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


    /// <summary>
    /// Builds the SDK project to validate compilation.
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> BuildSdkAsync(string packagePath, LanguageService languageService, CancellationToken ct)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(packagePath))
            {
                logger.LogError("Package path is null or empty in BuildSdkAsync");
                return (false, "Package path is required.");
            }

            if (!Directory.Exists(packagePath))
            {
                logger.LogError("Package path does not exist: {PackagePath}", packagePath);
                return (false, $"Path does not exist: {packagePath}");
            }

            logger.LogInformation("Building SDK for project path: {PackagePath}", packagePath);

            // Get repository root path from project path
            string sdkRepoRoot = gitHelper.DiscoverRepoRoot(packagePath);
            if (string.IsNullOrEmpty(sdkRepoRoot))
            {
                return (false, $"Failed to discover local sdk repo with project-path: {packagePath}.");
            }

            string sdkRepoName = gitHelper.GetRepoName(sdkRepoRoot);
            PackageInfo? packageInfo = await languageService.GetPackageInfo(packagePath, ct);
            // Return if the project is python project (Python SDKs don't require compilation)
            if (sdkRepoName.Contains("azure-sdk-for-python", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null); // Success - no build needed for Python
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
                    var result = await specGenSdkConfigHelper.ExecuteProcessAsync(processOptions, ct, packageInfo, "Build completed successfully.");
                    var errorMessage = result.ExitCode == 0 ? null : 
                        !string.IsNullOrEmpty(result.ResponseError) ? result.ResponseError :
                        result.ResponseErrors?.Count > 0 ? string.Join("; ", result.ResponseErrors) :
                        !string.IsNullOrEmpty(result.Message) ? result.Message :
                        $"Build failed with exit code {result.ExitCode}";
                    return (result.ExitCode == 0, errorMessage);
                }
            }
            
            return (false, "No build configuration found or failed to prepare the build command");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while building SDK");
            return (false, $"An error occurred: {ex.Message}");
        }
    }

    private async Task<bool> ApplyPatchesAsync(
        string commitSha,
        string? customizationRoot,
        string packagePath,
        LanguageService languageService,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
        {
            logger.LogInformation("No customizations found to patch");
            return false;
        }

        logger.LogInformation("Applying patches...");
        var patchesApplied = await languageService.ApplyPatchesAsync(commitSha, customizationRoot, packagePath, ct);
        logger.LogDebug("Patch application result: {Success}", patchesApplied);

        return patchesApplied;
    }

    private async Task BuildAndGenerateGuidanceAsync(
        string packagePath,
        LanguageService languageService,
        List<string> guidance,
        CancellationToken ct)
    {
        var (success, errorMessage) = await BuildSdkAsync(packagePath, languageService, ct);

        if (success)
        {
            logger.LogInformation("Build completed successfully - validation passed");
            guidance.Add(PATCHES_APPLIED_GUIDANCE);
        }
        else
        {
            logger.LogError("Build failed: {Error}", errorMessage);
            guidance.Add($"Build failed after applying patches: {errorMessage}");
            guidance.Add("");
            guidance.Add(PATCHES_FAILED_GUIDANCE);
        }
    }
}
