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
        var (success, errorMessage, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

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
