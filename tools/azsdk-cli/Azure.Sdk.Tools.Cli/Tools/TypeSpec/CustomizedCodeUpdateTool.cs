// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly FeedbackClassifierService feedbackClassifier;
    private readonly IAPIViewFeedbackCustomizationsHelpers feedbackHelper;
    private readonly ILoggerFactory loggerFactory;
    private readonly IMicroagentHostService _microagentHost;
    private readonly IConfiguration _configuration;
    private readonly string _model;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    /// <param name="feedbackClassifier">The feedback classifier for analyzing build errors and APIView feedback.</param>
    /// <param name="feedbackHelper">The feedback helper for extracting feedback from various sources.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="microagentHost">The microagent host service for enhanced guidance generation.</param>
    /// <param name="configuration">The configuration for model settings.</param>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        FeedbackClassifierService feedbackClassifier,
        IAPIViewFeedbackCustomizationsHelpers feedbackHelper,
        ILoggerFactory loggerFactory,
        IMicroagentHostService microagentHost,
        IConfiguration configuration
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
        this.feedbackClassifier = feedbackClassifier;
        this.feedbackHelper = feedbackHelper;
        this.loggerFactory = loggerFactory;
        _microagentHost = microagentHost;
        _configuration = configuration;
        _model = configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
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

    private readonly Argument<string> packagePathArg = new("package-path")
    {
        Description = "Path to the SDK package directory",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> tspProjectPathArg = new("tsp-project-path")
    {
        Description = "Path to the TypeSpec project directory",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string> apiViewUrlOption = new("--apiview-url")
    {
        Description = "APIView URL to fetch comments from for classification",
        Required = false
    };

    private readonly Option<string> plainTextFeedbackOption = new("--plain-text-feedback")
    {
        Description = "Plain text feedback for classification (e.g., build errors)",
        Required = false
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
       new McpCommand("customized-update", "Update customized TypeSpec-generated client code with automated patch analysis.", CustomizedCodeUpdateToolName)
       {
            updateCommitSha,
            packagePathArg,
            tspProjectPathArg,
            apiViewUrlOption,
            plainTextFeedbackOption
       };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var commitSha = parseResult.GetValue(updateCommitSha);
        var packagePath = parseResult.GetValue(packagePathArg);
        var tspProjectPath = parseResult.GetValue(tspProjectPathArg);
        var apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        var plainTextFeedback = parseResult.GetValue(plainTextFeedbackOption);
        
        try
        {
            logger.LogInformation("Starting client update for {packagePath}", packagePath);
            return await RunUpdateAsync(commitSha, packagePath, tspProjectPath, apiViewUrl, plainTextFeedback, ct);
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

    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Update customized TypeSpec-generated client code")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(
        string commitSha,
        string packagePath,
        string tspProjectPath,
        string? apiViewUrl = null,
        string? plainTextFeedback = null,
        CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, tspProjectPath, apiViewUrl, plainTextFeedback, ct);

    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(
        string commitSha,
        string packagePath,
        string tspProjectPath,
        string? apiViewUrl,
        string? plainTextFeedback,
        CancellationToken ct)
    {
        try
        {

            if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = $"Package path is required and must exist: {packagePath}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = $"Package path does not exist: {packagePath}" 
                };
            }

            if (string.IsNullOrEmpty(tspProjectPath) || !Directory.Exists(tspProjectPath))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = $"TypeSpec project path is required and must exist: {tspProjectPath}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = $"TypeSpec project path does not exist: {tspProjectPath}" 
                };
            }            
            // If feedback sources provided, use feedback-driven classification
            if (!string.IsNullOrEmpty(apiViewUrl) || !string.IsNullOrEmpty(plainTextFeedback))
            {
                // Create a classifier instance with the correct paths for tool operations
                var classifier = new FeedbackClassifierService(
                    _microagentHost,
                    _configuration,
                    loggerFactory,
                    tspProjectPath,
                    packagePath
                );

                // Step 1: Preprocess feedback
                var feedbackContext = await PreprocessFeedback(apiViewUrl, plainTextFeedback, ct);

                // Initialize tracking lists, which items set to customizable for now.
                var customizable = feedbackContext.FeedbackItems.ToList();
                var success = new List<FeedbackItem>();
                var failure = new List<FeedbackItem>();

                // Global context tracking all changes
                var globalContext = new System.Text.StringBuilder();

                // TODO: Truncate globalContext if needed later for token limits

                var language = feedbackContext.Language ?? ExtractLanguageFromPackagePath(packagePath);

                // Step 2: First classification (empty context - determines what needs TypeSpec work)
                logger.LogInformation("=== First Classification ===");
                await classifier.ClassifyAsync(customizable, globalContext.ToString(), language, null, null, ct);
                MoveItemsByStatus(customizable, success, failure);

                // TODO: TESTING - Output results and return early
                OutputResults(customizable, success, failure, globalContext.ToString());
                return BuildResponse(success, failure, customizable);

                // Step 3: Apply TypeSpec customizations
                logger.LogInformation("=== Applying TypeSpec Customizations ===");
                var customizationResult = await ApplyCustomizations(customizable, tspProjectPath, language, globalContext.ToString(), ct);
                
                globalContext.AppendLine("=== TypeSpec Customizations ===");
                globalContext.AppendLine(customizationResult);
                globalContext.AppendLine();
                
                // Mark all customizable items with the customization result
                foreach (var item in customizable)
                {
                    item.Context += $"\nTypeSpec Customizations: {customizationResult}";
                }

                // Step 4: Generate SDK from TypeSpec
                logger.LogInformation("=== Generating SDK ===");
                var generateResult = await GenerateSDK(tspProjectPath, language, globalContext.ToString(), ct);
                globalContext.AppendLine("=== SDK Generation ===");
                globalContext.AppendLine(generateResult.Success ? "Status: Success" : "Status: Failed");
                globalContext.AppendLine(generateResult.Summary);
                globalContext.AppendLine();

                // Step 5: Build SDK
                logger.LogInformation("=== Building SDK ===");
                var buildResult = await BuildSDK(packagePath, language, globalContext.ToString(), ct);
                globalContext.AppendLine("=== Build Result ===");
                globalContext.AppendLine(buildResult.Success ? "Status: Success" : "Status: Failed");
                globalContext.AppendLine(buildResult.Summary);
                globalContext.AppendLine();

                // If build failed, preprocess as plain text feedback
                if (!buildResult.Success)
                {
                    var buildErrorText = $"Build failed after applying TypeSpec customizations:\n{buildResult.Summary}";
                    var buildFeedbackContext = await PreprocessFeedback(null, buildErrorText, ct);
                    
                    if (buildFeedbackContext != null && buildFeedbackContext.FeedbackItems.Any())
                    {
                        customizable.AddRange(buildFeedbackContext.FeedbackItems);
                        
                        // Update original items to reference build failure
                        foreach (var item in customizable.Except(buildFeedbackContext.FeedbackItems))
                        {
                            item.Context += "\nBuild failed - see build error items";
                        }
                    }
                }
                else
                {
                    // Build succeeded - update items
                    foreach (var item in customizable)
                    {
                        item.Context += "\nBuild succeeded";
                    }
                }

                // Step 6: Second classification (with full context)
                logger.LogInformation("=== Second Classification ===");
                await classifier.ClassifyAsync(customizable, globalContext.ToString(), language, null, null, ct);
                MoveItemsByStatus(customizable, success, failure);

                // Step 7: Output final results
                OutputResults(customizable, success, failure, globalContext.ToString());
                return BuildResponse(success, failure, customizable);
            }

            // Otherwise, run standard update flow without classification
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = "Commit SHA is required.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = "Commit SHA is required." 
                };
            }
            return await RegenerateAndApplyCodeCustomizations(commitSha, packagePath, ct);
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

    /// <summary>
    /// Preprocesses feedback from APIView or plain text sources.
    /// </summary>
    private async Task<FeedbackContext?> PreprocessFeedback(
        string? apiViewUrl,
        string? plainTextFeedback,
        CancellationToken ct)
    {
        IFeedbackItem? feedbackItem = null;

        if (!string.IsNullOrEmpty(apiViewUrl))
        {
            var feedbackItemLogger = loggerFactory.CreateLogger<APIViewFeedbackItem>();
            feedbackItem = new APIViewFeedbackItem(apiViewUrl, feedbackHelper, feedbackItemLogger);
        }
        else if (!string.IsNullOrEmpty(plainTextFeedback))
        {
            var feedbackItemLogger = loggerFactory.CreateLogger<BuildLogFeedbackItem>();
            feedbackItem = new BuildLogFeedbackItem(plainTextFeedback, feedbackItemLogger);
        }

        var context = await feedbackItem.PreprocessAsync(ct);
        return context;
    }

    /// <summary>
    /// Moves items between lists based on their Status property.
    /// </summary>
    private void MoveItemsByStatus(
        List<FeedbackItem> customizable,
        List<FeedbackItem> success,
        List<FeedbackItem> failure)
    {
        foreach (var item in customizable.ToList())
        {
            if (item.Status == FeedbackStatus.SUCCESS)
            {
                success.Add(item);
                customizable.Remove(item);
            }
            else if (item.Status == FeedbackStatus.FAILURE)
            {
                failure.Add(item);
                customizable.Remove(item);
            }
        }
    }

    /// <summary>
    /// Builds the final response from classified items.
    /// </summary>
    private CustomizedCodeUpdateResponse BuildResponse(
        List<FeedbackItem> success,
        List<FeedbackItem> failure,
        List<FeedbackItem> customizable)
    {
        var classifications = new List<CustomizedCodeUpdateResponse.ItemClassificationDetails>();
        
        foreach (var item in success)
        {
            classifications.Add(new CustomizedCodeUpdateResponse.ItemClassificationDetails
            {
                ItemId = item.Id,
                Classification = "SUCCESS",
                Reason = !string.IsNullOrEmpty(item.Reason) ? item.Reason : "Item successfully resolved",
                NextAction = item.NextAction,
                Text = item.Text
            });
        }
        
        foreach (var item in failure)
        {
            classifications.Add(new CustomizedCodeUpdateResponse.ItemClassificationDetails
            {
                ItemId = item.Id,
                Classification = "FAILURE",
                Reason = !string.IsNullOrEmpty(item.Reason) ? item.Reason : "Item could not be resolved",
                NextAction = item.NextAction,
                Text = item.Text
            });
        }
        
        foreach (var item in customizable)
        {
            classifications.Add(new CustomizedCodeUpdateResponse.ItemClassificationDetails
            {
                ItemId = item.Id,
                Classification = "CUSTOMIZABLE",
                Reason = !string.IsNullOrEmpty(item.Reason) ? item.Reason : "Item still in progress",
                NextAction = item.NextAction,
                Text = item.Text
            });
        }

        return new CustomizedCodeUpdateResponse
        {
            Message = $"Processing complete: {success.Count} succeeded, {failure.Count} failed, {customizable.Count} customizable",
            Classifications = classifications
        };
    }

    /// <summary>
    /// Parses TypeSpec customization summary to extract addressed item IDs.
    /// </summary>
    private HashSet<string> ParseAddressedItems(string[] changesSummary)
    {
        var addressed = new HashSet<string>();
        var pattern = @"\(addresses:\s*([0-9,\s]+)\)";
        
        foreach (var change in changesSummary)
        {
            var match = System.Text.RegularExpressions.Regex.Match(change, pattern);
            if (match.Success)
            {
                var ids = match.Groups[1].Value
                    .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim());
                
                foreach (var id in ids)
                {
                    addressed.Add(id);
                }
            }
        }
        
        return addressed;
    }

    /// <summary>
    /// Gets the changes that were applied for a specific item.
    /// </summary>
    private List<string> GetChangesForItem(string itemId, string[] changesSummary)
    {
        var changes = new List<string>();
        var pattern = $@"\(addresses:[^)]*\b{itemId}\b[^)]*\)";
        
        foreach (var change in changesSummary)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(change, pattern))
            {
                // Extract the change text before the (addresses: ...) part
                var changeText = System.Text.RegularExpressions.Regex.Replace(
                    change,
                    @"\s*\(addresses:[^)]+\)\s*$",
                    ""
                ).Trim();
                
                changes.Add(changeText);
            }
        }
        
        return changes;
    }

    /// <summary>
    /// Applies TypeSpec customizations to address feedback items.
    /// </summary>
    private async Task<string> ApplyCustomizations(
        List<FeedbackItem> customizableItems,
        string tspProjectPath,
        string language,
        string globalContext,
        CancellationToken ct)
    {
        // TODO: Call ApplyTypeSpecCustomization service
        // TODO: Explore a more structured format for summaries
        
        try
        {
            // Placeholder - will be implemented with actual TypeSpec customization service
            await Task.CompletedTask;
            
            return "ApplyTypeSpecCustomization not yet implemented";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in ApplyCustomizations");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates SDK from TypeSpec project.
    /// </summary>
    private async Task<(bool Success, string Summary)> GenerateSDK(
        string tspProjectPath,
        string language,
        string globalContext,
        CancellationToken ct)
    {
        try
        {
            // TODO: Placeholder for real code
            var tspLocationPath = Path.Combine(tspProjectPath, "tsp-location.yaml");
            var result = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, tspProjectPath, null, isCli: false, ct);
            
            if (result.IsSuccessful)
            {
                return (true, "SDK generation succeeded");
            }
            else
            {
                return (false, $"SDK generation failed: {result.ResponseError}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in GenerateSDK");
            return (false, $"SDK generation exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the SDK and applies code repairs if needed.
    /// </summary>
    private async Task<(bool Success, string Summary)> BuildSDK(
        string packagePath,
        string language,
        string globalContext,
        CancellationToken ct)
    {
        // TODO: Build SDK and code repair
        try
        {
            var languageService = await GetLanguageServiceAsync(packagePath, ct);
            var (buildSuccess, buildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
            
            if (buildSuccess)
            {
                return (true, "Build succeeded");
            }
            else
            {
                return (false, $"Build failed: {buildError}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception in BuildSDK");
            return (false, $"Build exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Outputs all feedback items grouped by status (test run format).
    /// </summary>
    private void OutputResults(
        List<FeedbackItem> customizable,
        List<FeedbackItem> success,
        List<FeedbackItem> failure,
        string globalContext)
    {
        logger.LogInformation("\n========================================");
        logger.LogInformation("GLOBAL CONTEXT");
        logger.LogInformation("========================================");
        logger.LogInformation("{GlobalContext}", globalContext);
        logger.LogInformation("");

        if (success.Count > 0)
        {
            logger.LogInformation("========================================");
            logger.LogInformation("✓ SUCCESS ({Count} items)", success.Count);
            logger.LogInformation("========================================");
            foreach (var item in success)
            {
                logger.LogInformation("Item: {Id}", item.Id);
                logger.LogInformation("Text: {Text}", item.Text);
                logger.LogInformation("Context:\n{Context}", item.Context);
                logger.LogInformation("---");
            }
            logger.LogInformation("");
        }

        if (failure.Count > 0)
        {
            logger.LogInformation("========================================");
            logger.LogInformation("✗ FAILURE ({Count} items)", failure.Count);
            logger.LogInformation("========================================");
            foreach (var item in failure)
            {
                logger.LogInformation("Item: {Id}", item.Id);
                logger.LogInformation("Text: {Text}", item.Text);
                logger.LogInformation("Context:\n{Context}", item.Context);
                logger.LogInformation("---");
            }
            logger.LogInformation("");
        }

        if (customizable.Count > 0)
        {
            logger.LogInformation("========================================");
            logger.LogInformation("⧗ CUSTOMIZABLE ({Count} items)", customizable.Count);
            logger.LogInformation("========================================");
            foreach (var item in customizable)
            {
                logger.LogInformation("Item: {Id}", item.Id);
                logger.LogInformation("Text: {Text}", item.Text);
                logger.LogInformation("Context:\n{Context}", item.Context);
                logger.LogInformation("---");
            }
            logger.LogInformation("");
        }

        logger.LogInformation("========================================");
        logger.LogInformation("SUMMARY: {Success} succeeded, {Failure} failed, {Customizable} in progress",
            success.Count, failure.Count, customizable.Count);
        logger.LogInformation("========================================");
    }

    /// <summary>
    /// Applies code customizations: regenerates SDK from TypeSpec and patches customization files.
    /// </summary>
    private async Task<CustomizedCodeUpdateResponse> RegenerateAndApplyCodeCustomizations(
        string commitSha,
        string packagePath,
        CancellationToken ct)
    {
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

        var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
        var regenResult = await tspClientHelper.UpdateGenerationAsync(tspLocationPath, packagePath, commitSha, isCli: false, ct);
        if (!regenResult.IsSuccessful)
        {
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Regeneration failed: {regenResult.ResponseError}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateFailed,
                ResponseError = regenResult.ResponseError
            };
        }

        var hasCustomizations = languageService.HasCustomizations(packagePath, ct);
        logger.LogDebug("Has customizations: {HasCustomizations}", hasCustomizations);

        // Check if customizations exist - only activate Phase B if customization files are present
        if (!hasCustomizations)
        {
            logger.LogInformation("No customization files detected - validating build");
            
            // Still need to build to verify generated code compiles
            var (noCustomBuildSuccess, noCustomBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
            if (noCustomBuildSuccess)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = "Regeneration succeeded. No customization files found.",
                    NextSteps = SuccessNoCustomizationsNextSteps.ToList()
                };
            }
            else
            {
                logger.LogError("Build failed with no customizations: {Error}", noCustomBuildError);
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Build failed after regeneration: {noCustomBuildError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                    ResponseError = noCustomBuildError,
                    NextSteps = GetBuildNoCustomizationsFailedNextSteps(languageService.Language.ToString()).ToList()
                };
            }
        }

        logger.LogInformation("Customization files detected - activating Phase B");
        
        // Phase B: Apply patches to customization code
        var patchesApplied = await ApplyPatchesAsync(commitSha, packagePath, packagePath, languageService, ct);

        if (!patchesApplied)
        {
            // Customizations exist but patches were not applied
            logger.LogInformation("Patches were not applied. This may indicate no applicable changes or a patching error.");
            return new CustomizedCodeUpdateResponse
            {
                Message = "Patches not applied - automatic patching unsuccessful or not applicable.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                NextSteps = GetPatchesFailedNextSteps().ToList()
            };
        }

        // Patches were applied, regenerate and build to validate
        logger.LogInformation("Patches were applied. Regenerating code to validate customizations...");
        var (regenSuccess, regenError) = await RegenerateAfterPatchesAsync(tspLocationPath, packagePath, commitSha, ct);
        if (!regenSuccess)
        {
            logger.LogWarning("Code regeneration failed: {Error}", regenError);
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Code regeneration failed after applying patches: {regenError}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                ResponseError = regenError,
                NextSteps = GetPatchesFailedNextSteps().ToList()
            };
        }

        // Build to validate
        logger.LogInformation("Regeneration successful, building SDK code to validate...");
        var (buildSuccess, buildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

        if (buildSuccess)
        {
            logger.LogInformation("Build completed successfully - validation passed");
            return new CustomizedCodeUpdateResponse
            {
                Message = "Build passed. Patches applied successfully.",
                NextSteps = SuccessPatchesAppliedNextSteps.ToList()
            };
        }
        else
        {
            logger.LogError("Build failed: {Error}", buildError);
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Build failed after applying patches: {buildError}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                ResponseError = buildError,
                NextSteps = GetBuildAfterPatchesFailedNextSteps(buildError ?? "Unknown error").ToList()
            };
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAfterPatchesAsync(string tspLocationPath, string packagePath, string commitSha, CancellationToken ct)
    {
        try
        {
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha, isCli: false, ct);

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

    /// <summary>
    /// Extracts the language from a package path like "/path/to/azure-sdk-for-python".
    /// </summary>
    private static string ExtractLanguageFromPackagePath(string packagePath)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            packagePath, 
            @"azure-sdk-for-([a-z]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
}
