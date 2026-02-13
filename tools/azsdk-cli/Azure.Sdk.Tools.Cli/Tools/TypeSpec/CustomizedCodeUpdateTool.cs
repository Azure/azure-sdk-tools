// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[McpServerToolType, Description("Apply TypeSpec and SDK code customizations: updates client TypeSpec or SDK code, provides code update recommendations, and regenerates SDK packages")]
public class CustomizedCodeUpdateTool : LanguageMcpTool
/// <summary>
/// MCP tool that updates SDK code from TypeSpec, applies patches to customization files,
/// regenerates code, builds, and provides intelligent analysis and recommendations for updating customization code.
/// </summary>
[McpServerToolType, Description("Updates SDK code from TypeSpec, applies patches to customization files, regenerates code, builds, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool : LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";
    private const int CommandTimeoutInMinutes = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="languageServices">Available language services for SDK operations.</param>
    /// <param name="gitHelper">Helper for git operations.</param>
    /// <param name="tspClientHelper">Helper for TypeSpec client generation operations.</param>
    /// <param name="typeSpecCustomizationService">The TypeSpec customization service for applying client.tsp changes via AI agent.</param>
    /// <param name="typeSpecHelper">The TypeSpec helper for path validation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tspClientHelper"/> is null.</exception>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        ITypeSpecCustomizationService typeSpecCustomizationService,
        ITypeSpecHelper typeSpecHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper ?? throw new ArgumentNullException(nameof(tspClientHelper));
        this.typeSpecCustomizationService = typeSpecCustomizationService;
        this.typeSpecHelper = typeSpecHelper;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Argument<string> plainTextFeedbackArg = new("plain-text-feedback")
    {
        Description = "Text describing the customization to apply (build errors, API review feedback, user prompt, etc.)",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> typespecProjectPathArg = new("typespec-project-path")
    {
        Description = "Path to the TypeSpec project directory containing tspconfig.yaml",
        Arity = ArgumentArity.ExactlyOne
    };

    // NextSteps for success scenarios
    private static readonly string[] SuccessNoCustomizationsNextSteps =
    [
        "Review generated code changes",
        "Create customizations if needed for your SDK requirements (see: https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md)",
        "Open a pull request with your changes"
    ];

    private static readonly string[] SuccessPatchesAppliedNextSteps =
    [
        "Review applied changes in customization files",
        "Review generated code to ensure it meets your requirements",
        "Open a pull request with your changes"
    ];

    // NextSteps for failure scenarios - formatted for classifier to parse
    private static string[] GetBuildNoCustomizationsFailedNextSteps(string language) =>
    [
        "Issue: Build failed after regeneration but no customization files exist",
        $"SuggestedApproach: Create customization files for {language} to fix build errors",
        $"Documentation: {GetCodeCustomizationDocUrl(language)}"
    ];

    private static string[] GetPatchesFailedNextSteps() =>
    [
        "Issue: Automatic patching was unsuccessful or not applicable",
        "SuggestedApproach: Compare generated code with customizations and update manually",
        "Documentation: https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    ];

    private static string[] GetBuildAfterPatchesFailedNextSteps(string buildError) =>
    [
        "Issue: Build still failing after patches applied",
        $"BuildError: {buildError}",
        "SuggestedApproach: Review build errors and fix customization files manually",
        "Documentation: https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    ];

    private static string GetCodeCustomizationDocUrl(string language) => language.ToLowerInvariant() switch
    {
        "python" => "https://github.com/Azure/autorest.python/blob/main/docs/customizations.md",
        "java" => "https://github.com/Azure/autorest.java/blob/main/customization-base/README.md",
        "dotnet" => "https://github.com/microsoft/typespec/blob/main/packages/http-client-csharp/.tspd/docs/customization.md",
        "go" => "https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/development/generate.md",
        "javascript" or "typescript" => "https://github.com/Azure/azure-sdk-for-js/wiki/Modular-(DPG)-Customization-Guide",
        _ => "https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    };

    protected override Command GetCommand() =>
       new McpCommand("customized-update", "Apply TypeSpec and SDK code customizations with AI-assisted analysis.", CustomizedCodeUpdateToolName)
       {
            plainTextFeedbackArg, typespecProjectPathArg, SharedOptions.PackagePath,
       };
        new McpCommand("customized-update", "Update customized TypeSpec-generated client code, apply patches, regenerate, build, return result.", CustomizedCodeUpdateToolName)
        {
            SharedOptions.PackagePath,
        };

    /// <inheritdoc />
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var plainTextFeedback = parseResult.GetValue(plainTextFeedbackArg);
        var typespecProjectPath = parseResult.GetValue(typespecProjectPathArg);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath, nameof(packagePath));
        try
        {
            logger.LogInformation("Starting customization for {packagePath}", packagePath);
            return await RunUpdateAsync(plainTextFeedback, typespecProjectPath, packagePath, ct);
            logger.LogInformation("Starting customized code update for {PackagePath}", packagePath);
            return await RunUpdateAsync(packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customized code update failed");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = $"Customized code update failed: {ex.Message}",
                BuildResult = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError
            };
        }
    }

    /// <summary>
    /// MCP tool entry point — applies patches to customization files based on build errors,
    /// regenerates code if needed (Java), builds, and returns success/failure with build result.
    /// </summary>
    /// <param name="packagePath">Absolute path to the SDK package directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> indicating the outcome.</returns>
    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Applies patches to customization files based on build errors, regenerates code if needed (Java), builds, and returns success/failure with build result.")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(packagePath, ct);

    /// <summary>
    /// Executes the update pipeline: classify → patch customizations → regen → build.
    /// </summary>
    /// <param name="packagePath">Absolute path to the SDK package directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> with the pipeline result.</returns>
    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string packagePath, CancellationToken ct)
    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Update customized TypeSpec-generated client code")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string plainTextFeedback, string typespecProjectPath, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(plainTextFeedback, typespecProjectPath, packagePath, ct);

    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string plainTextFeedback, string typespecProjectPath, string packagePath, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Plain text feedback is required.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                    ResponseError = "Plain text feedback is required."
                };
            }
            if (!Directory.Exists(packagePath))
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Package path does not exist: {packagePath}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                    ResponseError = $"Package path does not exist: {packagePath}"
                };
            }
            if (!typeSpecHelper.IsValidTypeSpecProjectPath(typespecProjectPath))
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Invalid TypeSpec project path: {typespecProjectPath}. Directory must exist and contain tspconfig.yaml.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                    ResponseError = $"Invalid TypeSpec project path: {typespecProjectPath}"
                };
            }
            var languageService = await GetLanguageServiceAsync(packagePath, ct);
            if (languageService is null || !languageService.IsCustomizedCodeUpdateSupported)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Could not resolve a language service to perform SDK update.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService,
                    ResponseError = "Could not resolve a language service to perform SDK update."
                };
            }

            // --- TypeSpec Customizations (AI Agent) Phase ---
            logger.LogInformation("Applying TypeSpec customizations via AI agent");
            var tspCustomizationResult = await typeSpecCustomizationService.ApplyCustomizationAsync(
                typespecProjectPath, plainTextFeedback, ct: ct);

            if (!tspCustomizationResult.Success)
            {
                logger.LogWarning("TypeSpec customization failed: {Reason}", tspCustomizationResult.FailureReason);
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"TypeSpec customization failed: {tspCustomizationResult.FailureReason}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed,
                    ResponseError = tspCustomizationResult.FailureReason
                };
            }

            logger.LogInformation("TypeSpec customization succeeded. Changes: {Changes}", string.Join("; ", tspCustomizationResult.ChangesSummary));
            var typeSpecChanges = tspCustomizationResult.ChangesSummary.ToList();

            // Resolve the spec repo root from the TypeSpec project path.
            // tsp-client's --local-spec-repo expects the repo root, not the project subdirectory.
            var specRepoRoot = await gitHelper.DiscoverRepoRootAsync(typespecProjectPath, ct);
            logger.LogInformation("Resolved spec repo root: {RepoRoot} from project path: {ProjectPath}", specRepoRoot, typespecProjectPath);

            // --- Regenerate SDK using local spec repo ---
            var regenResult = await tspClientHelper.UpdateGenerationAsync(
                packagePath, localSpecRepoPath: specRepoRoot, isCli: false, ct: ct);

            if (!regenResult.IsSuccessful)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Regeneration failed: {regenResult.ResponseError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateFailed,
                    ResponseError = regenResult.ResponseError,
                    TypeSpecChangesSummary = typeSpecChanges
                };
            }

            // --- Build SDK ---
            logger.LogInformation("Building SDK to validate");
            var (buildSuccess, buildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

            if (buildSuccess)
            {
                logger.LogInformation("Build succeeded after TypeSpec customization");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Customization applied successfully. Build passed.",
                    TypeSpecChangesSummary = typeSpecChanges,
                    NextSteps = SuccessNoCustomizationsNextSteps.ToList()
                };
            }

            // --- Code Customizations (build failed) Phase ---
            logger.LogInformation("Build failed after TypeSpec customization - checking for code customizations");
            var hasCustomizations = languageService.HasCustomizations(packagePath, ct);
            logger.LogDebug("Has customizations: {HasCustomizations}", hasCustomizations);

            // Check if customizations exist - only activate Phase B if customization files are present
            if (!hasCustomizations)
            {
                logger.LogInformation("No customization files detected - returning manual guidance");
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Build failed after TypeSpec customization: {buildError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                    ResponseError = buildError,
                    TypeSpecChangesSummary = typeSpecChanges,
                    NextSteps = GetBuildNoCustomizationsFailedNextSteps(languageService.Language.ToString()).ToList()
                };
            }

            logger.LogInformation("Customization files detected - activating Phase B");
            // Phase B: Apply patches to customization code
            var patchesApplied = await ApplyPatchesAsync(packagePath, languageService, ct);

            if (!patchesApplied)
            {
                // Customizations exist but patches were not applied
                logger.LogInformation("Patches were not applied. This may indicate no applicable changes or a patching error.");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Build failed after TypeSpec customization and automatic patching was unsuccessful or not applicable.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                    ResponseError = buildError,
                    TypeSpecChangesSummary = typeSpecChanges,
                    NextSteps = GetPatchesFailedNextSteps().ToList()
                };
            }
        // Validate input
        if (!Directory.Exists(packagePath))
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = $"Package path does not exist: {packagePath}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"Package path does not exist: {packagePath}"
            };
        }

        var languageService = await GetLanguageServiceAsync(packagePath, ct);
        if (!languageService.IsCustomizedCodeUpdateSupported)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "Language service does not support customized code updates.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService,
                BuildResult = "No language service available for this package type."
            };
        }

        // Step 1: Initial build to get current errors
        logger.LogInformation("Running initial build...");
        var (initialBuildSuccess, initialBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

        if (initialBuildSuccess)
        {
            logger.LogInformation("Build passed - no repairs needed.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed - no repairs needed."
            };
        }

        // Step 2: Check for customization files to repair
        var customizationRoot = languageService.HasCustomizations(packagePath, ct);
        if (customizationRoot == null)
        {
            logger.LogInformation("Build failed but no customization files found.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "Build failed but no customization files found to repair.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                BuildResult = initialBuildError
            };
        }

        // Step 3: Apply patches based on build errors
        logger.LogInformation("Applying patches to fix build errors...");
        var patches = await languageService.ApplyPatchesAsync(
            customizationRoot,
            packagePath,
            initialBuildError!,
            ct);

        if (patches.Count == 0)
        {
            logger.LogInformation("No patches applied.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "No patches could be applied - automated repair found nothing to fix.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                BuildResult = initialBuildError
            };
        }

        // Step 4: Regenerate if Java (only Java needs regen after patching customization files)
        if (languageService.Language == SdkLanguage.Java)
        {
            logger.LogInformation("Regenerating code after patches (Java)...");
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha: null, isCli: false, ct);
            if (!regenResult.IsSuccessful)
            {
                logger.LogWarning("Regeneration failed: {Error}", regenResult.ResponseError);
                return new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    Message = $"Regeneration failed after patches: {regenResult.ResponseError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                    BuildResult = regenResult.ResponseError,
                    AppliedPatches = patches
                };
            }
        }

        // Step 5: Final build to validate
        logger.LogInformation("Running final build to validate...");
        var (finalBuildSuccess, finalBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

        if (finalBuildSuccess)
        {
            logger.LogInformation("Build passed after repairs.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed after repairs.",
                AppliedPatches = patches
            };
        }

        // Build still failing
        logger.LogInformation("Build still failing after patches.");
        return new CustomizedCodeUpdateResponse
        {
            Success = false,
            Message = "Patches applied but build still failing.",
            ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
            BuildResult = finalBuildError,
            AppliedPatches = patches
        };
    }
}
