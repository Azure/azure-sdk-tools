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

[McpServerToolType, Description("Updates SDK code from TypeSpec and automatically repairs customization files when build fails.")]
public class CustomizedCodeUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
    }

    // MCP Tool Names
    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";
    private const int CommandTimeoutInMinutes = 30;

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Argument<string> updateCommitSha = new("update-commit-sha")
    {
        Description = "SHA of the commit to apply update changes for",
        Arity = ArgumentArity.ExactlyOne
    };

    private const string DefaultCustomizationDocUrl = "https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md";

    private static readonly string[] SuccessNextSteps =
    [
        "Review changes and open a pull request"
    ];

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
        $"Documentation: {DefaultCustomizationDocUrl}"
    ];

    private static string[] GetMaxIterationsReachedNextSteps(string buildError) =>
    [
        "## Status: Partial Success",
        "",
        "Customization patches were applied, but build errors remain.",
        "",
        "## Remaining Error",
        buildError,
        "",
        "## Analysis",
        "The remaining errors are in **generated code** (not customization files).",
        "This tool can only patch customization files - it cannot modify generated code.",
        "",
        "## Common Causes",
        "- TypeSpec renamed a property, and the emitter's partial-update kept handwritten",
        "  constructors/methods that reference the old name",
        "- Generated code has internal inconsistencies the emitter should have resolved",
        "",
        "## Recommended Actions",
        "- If error is in generated code: File an issue with the language emitter",
        "- If error is in customization: Review the patch that was applied and adjust manually",
        "",
        "## Documentation",
        "https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    ];

    private static string GetCodeCustomizationDocUrl(string language) => language.ToLowerInvariant() switch
    {
        "python" => "https://github.com/Azure/autorest.python/blob/main/docs/customizations.md",
        "java" => "https://github.com/Azure/autorest.java/blob/main/customization-base/README.md",
        "dotnet" => "https://github.com/microsoft/typespec/blob/main/packages/http-client-csharp/.tspd/docs/customization.md",
        "go" => "https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/development/generate.md",
        "javascript" => "https://github.com/Azure/azure-sdk-for-js/wiki/Modular-(DPG)-Customization-Guide",
        _ => "https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    };
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
            return new CustomizedCodeUpdateResponse 
            { 
                Message = $"SDK update failed: {ex.Message}",
                ResponseError = ex.Message, 
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError 
            };
        }
    }

    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Updates SDK code from a TypeSpec commit and automatically repairs customization files (e.g., *Customization.java, _patch.py, partial classes) when the build fails. Returns success if build passes, or detailed error analysis with applied patches if issues remain.")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, ct);

    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string commitSha, string packagePath, CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = $"Package path does not exist: {packagePath}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = $"Package path does not exist: {packagePath}" 
                };
            }
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = "Commit SHA is required.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = "Commit SHA is required." 
                };
            }
            var languageService = await GetLanguageServiceAsync(packagePath, ct);
            if (!languageService.IsCustomizedCodeUpdateSupported)
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = "Could not resolve a language service to perform SDK update.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService, 
                    ResponseError = "Could not resolve a language service to perform SDK update." 
                };
            }

            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha, isCli: false, ct);
            if (!regenResult.IsSuccessful)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Regeneration failed: {regenResult.ResponseError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateFailed,
                    ResponseError = regenResult.ResponseError
                };
            }

            // Step 1: Build to check if customizations are already compatible
            logger.LogInformation("[STAGE] Starting initial build to check compatibility...");
            var (initialBuildSuccess, initialBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

            if (initialBuildSuccess)
            {
                logger.LogInformation("[STAGE] Build passed - no repairs needed");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Build passed.",
                    NextSteps = SuccessNextSteps.ToList()
                };
            }

            // Build failed - check if we have customizations to repair
            logger.LogInformation("[STAGE] Build failed, checking for customizations to repair...");
            var customizationRoot = languageService.HasCustomizations(packagePath, ct);

            if (customizationRoot == null)
            {
                // Build failed but no customization files to repair
                logger.LogDebug("Build failed, no customizations to repair");
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Build failed after regeneration: {initialBuildError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                    ResponseError = initialBuildError,
                    NextSteps = GetBuildNoCustomizationsFailedNextSteps(languageService.Language.ToString()).ToList()
                };
            }

            // Error-driven repair: attempt to fix build errors automatically
            logger.LogInformation("[STAGE] Found customizations at: {CustomizationRoot}", customizationRoot);
            logger.LogInformation("[STAGE] Applying error-driven patches...");

            // Pass build error to microagent for repair
            var patches = await ApplyPatchesAsync(
                commitSha, 
                customizationRoot, 
                packagePath, 
                languageService, 
                initialBuildError!,
                ct);

            if (patches.Count == 0)
            {
                logger.LogInformation("[STAGE] No patches applied - automatic patching found nothing to fix.");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "No patches applied - automatic patching found nothing to fix.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                    ResponseError = initialBuildError,
                    AppliedPatches = null,
                    NextSteps = GetPatchesFailedNextSteps().ToList()
                };
            }

            // Regenerate after patches
            logger.LogInformation("[STAGE] Regenerating code after patches...");
            var (regenSuccess, regenError) = await RegenerateAfterPatchesAsync(packagePath, commitSha, ct);
            if (!regenSuccess)
            {
                logger.LogInformation("[STAGE] Code regeneration failed: {Error}", regenError);
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Code regeneration failed after applying patches: {regenError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                    ResponseError = regenError,
                    NextSteps = GetPatchesFailedNextSteps().ToList()
                };
            }

            // Build to validate
            logger.LogInformation("[STAGE] Validating build after patches...");
            var (postPatchBuildSuccess, postPatchBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

            if (postPatchBuildSuccess)
            {
                logger.LogInformation("[STAGE] Build passed after repairs.");
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Build passed after repairs.",
                    AppliedPatches = patches,
                    NextSteps = SuccessNextSteps.ToList()
                };
            }

            // Build still failing after patches - remaining errors are likely in generated code
            logger.LogInformation("[STAGE] Build still failing after patches. Remaining errors likely in generated code.");
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Patches applied but build still failing: {postPatchBuildError}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                ResponseError = postPatchBuildError,
                AppliedPatches = patches,
                NextSteps = GetMaxIterationsReachedNextSteps(postPatchBuildError ?? "Unknown error").ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Core update failed");
            return new CustomizedCodeUpdateResponse 
            { 
                Message = $"Core update failed: {ex.Message}",
                ResponseError = ex.Message, 
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError 
            };
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAfterPatchesAsync(string packagePath, string commitSha, CancellationToken ct)
    {
        try
        {
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha, isCli: false, ct);

            if (!regenResult.IsSuccessful)
            {
                logger.LogWarning("Code regeneration failed: {Error}", regenResult.ResponseError);
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


    private async Task<List<AppliedPatch>> ApplyPatchesAsync(
        string commitSha,
        string? customizationRoot,
        string packagePath,
        LanguageService languageService,
        string buildError,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(customizationRoot) || !Directory.Exists(customizationRoot))
        {
            logger.LogInformation("No customizations found to patch");
            return [];
        }

        // Enrich build error with context for LLM understanding
        var enrichedContext = BuildEnrichedErrorContext(commitSha, buildError);
        
        var patches = await languageService.ApplyPatchesAsync(commitSha, customizationRoot, packagePath, enrichedContext, ct);
        logger.LogDebug("Patches applied: {PatchCount}", patches.Count);

        return patches;
    }

    /// <summary>
    /// Builds enriched context for the LLM to understand what happened and what to fix.
    /// </summary>
    private static string BuildEnrichedErrorContext(string commitSha, string buildError)
    {
        return $"""
            ## CONTEXT
            - TypeSpec regenerated to commit: {commitSha}

            ## BUILD ERROR
            {buildError}
            """;
    }
}
