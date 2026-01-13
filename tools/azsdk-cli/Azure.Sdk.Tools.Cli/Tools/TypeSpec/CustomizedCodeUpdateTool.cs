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
    /// </summary>
    /// <param name="packagePath">Path to the package directory.</param>
    /// <param name="languageService">The language service for the package.</param>
    /// <returns>
    /// True if customization files are detected.
    /// False if no customizations detected or detection not supported for the language.
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
                    return HasJavaScriptCustomizationFiles(packagePath);

                case SdkLanguage.Go:
                    return HasGoCustomizationFiles(packagePath);

                default:
                    logger.LogWarning("Unknown language for customization detection: {Language}. " +
                        "Customization detection not supported for this language.", languageService.Language);
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error detecting customization files for {Language} in {PackagePath}.",
                languageService.Language, packagePath);
            return false;
        }
    }

    /// <summary>
    /// Checks for Java customization files: /customization/ directory.
    /// </summary>
    private bool HasJavaCustomizationFiles(string packagePath)
    {
        var customizationDir = Path.Combine(packagePath, "customization");
        if (Directory.Exists(customizationDir))
        {
            logger.LogDebug("Found Java customization directory: {CustomizationDir}", customizationDir);
            return true;
        }

        logger.LogDebug("No Java customization directory found in {PackagePath}", packagePath);
        return false;
    }

    /// <summary>
    /// Checks for Python customization files: *_patch.py files.
    /// </summary>
    private bool HasPythonCustomizationFiles(string packagePath)
    {
        var patchFiles = Directory.GetFiles(packagePath, "*_patch.py", SearchOption.AllDirectories);
        if (patchFiles.Length > 0)
        {
            logger.LogDebug("Found {Count} Python patch files", patchFiles.Length);
            return true;
        }

        logger.LogDebug("No Python patch files found in {PackagePath}", packagePath);
        return false;
    }

    /// <summary>
    /// Checks for .NET customization files: partial classes in .cs files.
    /// </summary>
    private bool HasDotNetCustomizationFiles(string packagePath)
    {
        var csFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("partial class", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Found .NET partial class in: {FilePath}", file);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                // Continue checking other files
            }
        }

        logger.LogDebug("No .NET partial classes found in {PackagePath}", packagePath);
        return false;
    }

    /// <summary>
    /// Checks for JavaScript/TypeScript customization files: generated/ folder at package root.
    /// </summary>
    private bool HasJavaScriptCustomizationFiles(string packagePath)
    {
        var generatedDir = Path.Combine(packagePath, "generated");
        if (Directory.Exists(generatedDir))
        {
            logger.LogDebug("Found JavaScript generated/ directory: {GeneratedDir}", generatedDir);
            return true;
        }

        logger.LogDebug("No JavaScript generated/ directory found in {PackagePath}", packagePath);
        return false;
    }

    /// <summary>
    /// Checks for Go customization files: go-generate command in tspconfig.yaml.
    /// </summary>
    private bool HasGoCustomizationFiles(string packagePath)
    {
        // Check for tspconfig.yaml in the package directory
        // check tsp-config.yaml for go-generate to detect customizations
        var tspConfigPath = Path.Combine(packagePath, "tspconfig.yaml");
        if (!File.Exists(tspConfigPath))
        {
            logger.LogDebug("No tspconfig.yaml found in {PackagePath} - cannot check for Go customizations", packagePath);
            return false;
        }

        try
        {
            var tspConfig = File.ReadAllText(tspConfigPath);
            // Check for go-generate in emitter options
            if (tspConfig.Contains("go-generate:", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug("Found go-generate command in {TspConfigPath}", tspConfigPath);
                return true;
            }

            logger.LogDebug("No go-generate command found in {TspConfigPath}", tspConfigPath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read tspconfig.yaml for Go customization detection in {PackagePath}", packagePath);
            return false;
        }
    }
}

