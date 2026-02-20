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
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly ITypeSpecCustomizationService typeSpecCustomizationService;
    private readonly ITypeSpecHelper typeSpecHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    /// <param name="typeSpecCustomizationService">The TypeSpec customization service for applying client.tsp changes via AI agent.</param>
    /// <param name="typeSpecHelper">The TypeSpec helper for path validation.</param>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        ITypeSpecCustomizationService typeSpecCustomizationService,
        ITypeSpecHelper typeSpecHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
        this.typeSpecCustomizationService = typeSpecCustomizationService;
        this.typeSpecHelper = typeSpecHelper;
    }

    // MCP Tool Names
    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";
    private const int CommandTimeoutInMinutes = 30;

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

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var plainTextFeedback = parseResult.GetValue(plainTextFeedbackArg);
        var typespecProjectPath = parseResult.GetValue(typespecProjectPathArg);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        try
        {
            logger.LogInformation("Starting customization for {packagePath}", packagePath);
            return await RunUpdateAsync(plainTextFeedback, typespecProjectPath, packagePath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Client customization failed");
            return new CustomizedCodeUpdateResponse
            {
                Message = $"SDK customization failed: {ex.Message}",
                ResponseError = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError
            };
        }
    }

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

            // Patches were applied, regenerate and build to validate
            logger.LogInformation("Patches were applied. Regenerating code to validate customizations...");
            var (regenAfterPatchSuccess, regenAfterPatchError) = await RegenerateAsync(packagePath, specRepoRoot, ct);
            if (!regenAfterPatchSuccess)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Code regeneration failed after applying patches: {regenAfterPatchError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                    ResponseError = regenAfterPatchError,
                    TypeSpecChangesSummary = typeSpecChanges,
                    NextSteps = GetPatchesFailedNextSteps().ToList()
                };
            }

            // Build to validate
            logger.LogInformation("Regeneration successful, building SDK code to validate...");
            var (buildAfterPatchSuccess, buildAfterPatchError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
            if (buildAfterPatchSuccess)
            {
                logger.LogInformation("Build completed successfully after applying patches");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Customization applied successfully. Build passed after patches applied.",
                    TypeSpecChangesSummary = typeSpecChanges,
                    NextSteps = SuccessPatchesAppliedNextSteps.ToList()
                };
            }

            logger.LogError("Build failed after applying patches: {Error}", buildAfterPatchError);
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Build failed after applying patches: {buildAfterPatchError}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                ResponseError = buildAfterPatchError,
                TypeSpecChangesSummary = typeSpecChanges,
                NextSteps = GetBuildAfterPatchesFailedNextSteps(buildAfterPatchError ?? "Unknown error").ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customization workflow failed");
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Customization workflow failed: {ex.Message}",
                ResponseError = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError
            };
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAsync(string packagePath, string specRepoRoot, CancellationToken ct)
    {
        try
        {
            var regenResult = await tspClientHelper.UpdateGenerationAsync(
                packagePath, localSpecRepoPath: specRepoRoot, isCli: false, ct: ct);

            if (!regenResult.IsSuccessful)
            {
                logger.LogError("Code regeneration failed: {Error}", regenResult.ResponseError);
                return (false, regenResult.ResponseError ?? "Code regeneration failed");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during code regeneration");
            return (false, ex.Message);
        }
    }

    private async Task<bool> ApplyPatchesAsync(
        string packagePath,
        LanguageService languageService,
        CancellationToken ct)
    {
        if (!Directory.Exists(packagePath))
        {
            logger.LogInformation("No customizations found to patch");
            return false;
        }

        logger.LogInformation("Applying patches...");
        var patchesApplied = await languageService.ApplyPatchesAsync(string.Empty, packagePath, packagePath, ct);
        logger.LogDebug("Patch application result: {Success}", patchesApplied);

        return patchesApplied;
    }
}
