// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides intelligent analysis and recommendations for updating customization code.")]
public class TspClientUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    public TspClientUpdateTool(
        ILogger<TspClientUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
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
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = "ClientUpdateFailed" };
        }
    }

    [McpServerTool(Name = "azsdk_tsp_update"), Description("Update customized TypeSpec-generated client code")]
    public Task<TspClientUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, ct);

    private async Task<TspClientUpdateResponse> RunUpdateAsync(string commitSha, string packagePath, CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(packagePath))
            {
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = $"Package path does not exist: {packagePath}" };
            }
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new TspClientUpdateResponse { ErrorCode = "1", ResponseError = "Commit SHA is required." };
            }
            var languageService = GetLanguageService(packagePath);
            if (!languageService.IsTspClientupdatedSupported)
            {
                return new TspClientUpdateResponse { ErrorCode = "NoLanguageService", ResponseError = "Could not resolve a client update language service." };
            }

            return await UpdateCoreAsync(commitSha, packagePath, languageService, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
        }
    }

    private async Task<TspClientUpdateResponse> UpdateCoreAsync(string commitSha, string packagePath, LanguageService languageService, CancellationToken ct)
    {
        try
        {
            var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
            logger.LogInformation("Regenerating code...");
            var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, commitSha, isCli: false, ct);
            if (!regenResult.IsSuccessful)
            {
                return new TspClientUpdateResponse
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
                    var (validationSuccess, validationRequiresReview) = await ValidateAndUpdateGuidanceAsync(packagePath, languageService, guidance, ct);
                    if (!validationSuccess)
                    {
                        requiresReview = true;
                    }
                    requiresReview = requiresReview || validationRequiresReview;
                }
            }

            return new TspClientUpdateResponse
            {
                NextSteps = guidance
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Core update failed");
            return new TspClientUpdateResponse { ResponseError = ex.Message, ErrorCode = ex.GetType().Name };
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
        var validationResult = await languageService.ValidateAsync(packagePath, ct);

        if (validationResult.Success)
        {
            logger.LogInformation("Validation passed");
            guidance.Insert(0, "Code regenerated and validated successfully after applying patches.");
            guidance.Insert(1, "");
            return (true, false);
        }
        else
        {
            logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationResult.Errors));
            guidance.Insert(0, "Code regenerated but validation failed after applying patches.");
            guidance.Insert(1, $"Validation errors: {string.Join(", ", validationResult.Errors)}");
            guidance.Insert(2, "");
            return (false, true);
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
