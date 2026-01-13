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

            // Apply patches
            var patchesApplied = await ApplyPatchesAsync(commitSha, customizationRoot, packagePath, languageService, ct);
            var guidance = new List<string>();

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
                // No patches applied or no customizations found
                guidance.Add(string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot) 
                    ? NO_CUSTOMIZATIONS_FOUND_NEXT_STEPS 
                    : PATCHES_FAILED_GUIDANCE);
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

    /// <summary>
    /// Detects whether customization files exist for the current language in the package directory.
    /// This determines if Phase B (code customization repairs) should activate after Phase A build failures.
    /// 
    /// Detection Confidence Levels:
    /// - Java: HIGH - /customization/ directory or *Customization.java files
    /// - Python: HIGH - *_patch.py files
    /// - .NET: MEDIUM - partial class keyword search (may have false positives)
    /// - JavaScript: NOT SUPPORTED - uses 3-way merge, not file-based detection
    /// - Go: NOT IMPLEMENTED - patterns not yet defined
    /// </summary>
    /// <param name="packagePath">Path to the package directory.</param>
    /// <param name="languageService">The language service for the package.</param>
    /// <returns>
    /// True if customization files exist and Phase B should be attempted.
    /// False if no customizations detected OR detection not supported for language.
    /// Note: False does NOT mean "no customizations exist" - it means "Phase B cannot help".
    /// </returns>
    private bool HasCustomizationFiles(string packagePath, LanguageService languageService)
    {
        if (!Directory.Exists(packagePath))
        {
            logger.LogWarning("Cannot detect customization files - package path does not exist: {PackagePath}", packagePath);
            return false;
        }

        try
        {
            switch (languageService.Language)
            {
                case SdkLanguage.Java:
                    return HasJavaCustomizationFiles(packagePath);

                case SdkLanguage.Python:
                    return HasPythonCustomizationFiles(packagePath);

                case SdkLanguage.DotNet:
                    return HasDotNetCustomizationFiles(packagePath);

                case SdkLanguage.JavaScript:
                    // JavaScript/TypeScript uses a different customization paradigm:
                    // - Authors edit generated code directly (not separate files)
                    // - Changes are reconciled via 3-way merge on regeneration
                    // - Detection would require git history analysis (comparing working dir vs. last generation)
                    // - Phase B's string replacement approach doesn't apply to merge-based workflows
                    // See: https://github.com/Azure/azure-sdk-for-js/wiki/Modular-(DPG)-Customization-Guide
                    // Future support would need: git-aware detection + 3-way merge handling
                    logger.LogDebug("JavaScript uses 3-way merge customization - Phase B detection not applicable. " +
                        "Returning false does NOT mean no customizations exist.");
                    return false;

                case SdkLanguage.Go:
                    // Go customization patterns not yet defined for Phase B scope
                    logger.LogDebug("Go customization detection not implemented yet. " +
                        "If you need Go support, please define customization file patterns first.");
                    return false;

                default:
                    logger.LogWarning("Unknown language for customization detection: {Language}. " +
                        "Returning false - Phase B will not activate. If this language needs support, " +
                        "implement detection logic in HasCustomizationFiles().", languageService.Language);
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error detecting customization files for {Language} in {PackagePath}. " +
                "Returning false to prevent Phase B activation. This may be a file system issue or permission problem.",
                languageService.Language, packagePath);
            return false;
        }
    }

    /// <summary>
    /// Checks for Java customization files: /customization/ directory OR *Customization.java files.
    /// 
    /// Detection Strategy (HIGH Confidence):
    /// - Looks for /customization/ directory (primary pattern)
    /// - OR looks for *Customization.java files anywhere in package tree
    /// 
    /// Limitations:
    /// - May miss customizations if naming convention differs
    /// - Does not inspect file contents to verify they are actual customizations
    /// - If this returns false but you know customizations exist, the file patterns may need updating
    /// </summary>
    private bool HasJavaCustomizationFiles(string packagePath)
    {
        // Check for /customization/ directory
        var customizationDir = Path.Combine(packagePath, "customization");
        if (Directory.Exists(customizationDir))
        {
            logger.LogDebug("Found Java customization directory: {CustomizationDir}. Phase B can proceed.", customizationDir);
            return true;
        }

        // Check for *Customization.java files anywhere in package
        var customizationFiles = Directory.GetFiles(packagePath, "*Customization.java", SearchOption.AllDirectories);
        if (customizationFiles.Length > 0)
        {
            logger.LogDebug("Found {Count} Java customization class files. Phase B can proceed.", customizationFiles.Length);
            return true;
        }

        logger.LogDebug("No Java customization files found in {PackagePath} using patterns: /customization/ directory or *Customization.java files. " +
            "If customizations exist with different naming, please update detection patterns.",
            packagePath);
        return false;
    }

    /// <summary>
    /// Checks for Python customization files: *_patch.py files.
    /// 
    /// Detection Strategy (HIGH Confidence):
    /// - Looks for *_patch.py files anywhere in package tree (standard Python customization pattern)
    /// 
    /// Limitations:
    /// - Only detects _patch.py naming convention
    /// - Does not validate if files contain actual customization code
    /// - If this returns false but you know customizations exist, they may use a different pattern
    /// </summary>
    private bool HasPythonCustomizationFiles(string packagePath)
    {
        var patchFiles = Directory.GetFiles(packagePath, "*_patch.py", SearchOption.AllDirectories);
        if (patchFiles.Length > 0)
        {
    /// <summary>
    /// Checks for .NET customization files: partial classes in .cs files.
    /// 
    /// Detection Strategy (MEDIUM Confidence):
    /// - Scans all .cs files for "partial class" keyword (case-insensitive)
    /// - Assumes partial classes indicate customization (common pattern in generated code)
    /// 
    /// Limitations and Uncertainty:
    /// - KEYWORD SEARCH ONLY: May produce false positives if partial classes exist for other reasons
    /// - Does NOT verify if partial classes are extending generated types vs. unrelated code
    /// - Does NOT check if customizations are in a specific directory structure
    /// - If this returns true but there are no actual customizations, the keyword search may be too broad
    /// - If this returns false but customizations exist, they may not use partial classes
    /// 
    /// Recommended Improvements:
    /// - Consider checking for specific file naming patterns (e.g., *.Customization.cs)
    /// - Or check for customizations in a dedicated directory (e.g., /Customizations/)
    /// - Or validate that partial classes extend known generated types
    /// </summary>
    private bool HasDotNetCustomizationFiles(string packagePath)
    {
        // Find all .cs files in the package
        var csFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            try
            {
                // Read file and check for "partial class" keyword
                var content = File.ReadAllText(file);
                if (content.Contains("partial class", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Found .NET partial class in: {FilePath}. CAUTION: This is a keyword search and may not definitively indicate customizations. " +
                        "Phase B will proceed, but if you encounter issues, verify these are actual customization files.",
                        file);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection. Skipping this file and continuing search. " +
                    "This may be a permission issue or the file may be locked.",
                    file);
                // Continue checking other files
            }
        }

        logger.LogDebug("No .NET partial classes found in {PackagePath} using keyword search. " +
            "If customizations exist but don't use partial classes, consider updating detection strategy.",
            packagePath);
        return false;
    }       }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                // Continue checking other files
            }
        }

        logger.LogDebug("No .NET partial classes found in {PackagePath}", packagePath);
        return false;
    }
}
