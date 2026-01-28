// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
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
    private readonly FeedbackClassifier feedbackClassifier;
    private readonly IAPIViewFeedbackCustomizationsHelpers feedbackHelper;
    private readonly ILoggerFactory loggerFactory;
    
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
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        FeedbackClassifier feedbackClassifier,
        IAPIViewFeedbackCustomizationsHelpers feedbackHelper,
        ILoggerFactory loggerFactory
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
        this.feedbackClassifier = feedbackClassifier;
        this.feedbackHelper = feedbackHelper;
        this.loggerFactory = loggerFactory;
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

    private readonly Option<string> apiViewUrlOption = new("--apiview-url")
    {
        Description = "APIView URL to fetch comments from for classification",
        Required = false
    };

    private readonly Option<string> buildLogOption = new("--build-log")
    {
        Description = "Path to build log file for classification",
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
            SharedOptions.PackagePath,
            apiViewUrlOption,
            buildLogOption
       };

    private async Task<string?> DiscoverPackagePathAsync(string? language, string? packageName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(packageName))
        {
            logger.LogWarning("Cannot discover package path without language and package name");
            return null;
        }

        // Map language to repo name
        var repoName = language.ToLowerInvariant() switch
        {
            "python" => "azure-sdk-for-python",
            "java" => "azure-sdk-for-java",
            "javascript" or "typescript" => "azure-sdk-for-js",
            "csharp" or "dotnet" => "azure-sdk-for-net",
            "go" => "azure-sdk-for-go",
            _ => null
        };

        if (repoName == null)
        {
            logger.LogWarning("Unknown language for repo discovery: {Language}", language);
            return null;
        }

        // Try to discover repo root
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var repoRoot = await gitHelper.DiscoverRepoRootAsync(currentDir, ct);
            
            // Verify this is the correct repo
            var discoveredRepoName = await gitHelper.GetRepoNameAsync(repoRoot, ct);
            if (!discoveredRepoName.Equals(repoName, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Discovered repo {DiscoveredRepo} does not match expected repo {ExpectedRepo}", 
                    discoveredRepoName, repoName);
                
                // Try common locations
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var commonPaths = new[]
                {
                    Path.Combine(homeDir, "repos", repoName),
                    Path.Combine(homeDir, repoName),
                    Path.Combine(homeDir, "source", repoName),
                    Path.Combine(homeDir, "src", repoName)
                };

                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path))
                    {
                        repoRoot = path;
                        logger.LogInformation("Found repo at {RepoRoot}", repoRoot);
                        break;
                    }
                }
            }

            // Construct package path: {repo_root}/sdk/{package_name}
            // Search recursively under sdk/ since package may be nested (e.g., sdk/contoso/azure-contoso-widgetmanager)
            var sdkDir = Path.Combine(repoRoot, "sdk");
            if (!Directory.Exists(sdkDir))
            {
                logger.LogWarning("SDK directory does not exist: {SdkDir}", sdkDir);
                return null;
            }

            // Search for package directory recursively
            var matchingDirs = Directory.GetDirectories(sdkDir, packageName, SearchOption.AllDirectories);
            
            if (matchingDirs.Length == 0)
            {
                logger.LogWarning("Package directory not found under {SdkDir}: {PackageName}", sdkDir, packageName);
                return null;
            }
            
            if (matchingDirs.Length > 1)
            {
                logger.LogWarning("Multiple matches found for {PackageName}, using first: {Path}", packageName, matchingDirs[0]);
            }
            
            var packagePath = matchingDirs[0];
            logger.LogInformation("Discovered package path: {PackagePath}", packagePath);
            return packagePath;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to discover package path for {Language}/{PackageName}", 
                language, packageName);
            return null;
        }
    }

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var spec = parseResult.GetValue(updateCommitSha);
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        var apiViewUrl = parseResult.GetValue(apiViewUrlOption);
        var buildLogPath = parseResult.GetValue(buildLogOption);
        
        try
        {
            logger.LogInformation("Starting client update for {packagePath}", packagePath ?? "<auto-discover>");
            return await RunUpdateAsync(spec, packagePath, apiViewUrl, buildLogPath, ct);
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
        string? packagePath = null, 
        string? apiViewUrl = null, 
        string? buildLogPath = null, 
        CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, apiViewUrl, buildLogPath, ct);

    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(
        string commitSha, 
        string? packagePath, 
        string? apiViewUrl, 
        string? buildLogPath, 
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commitSha))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = "Commit SHA is required.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = "Commit SHA is required." 
                };
            }

            // If feedback sources provided, use classification retry loop
            if (!string.IsNullOrEmpty(apiViewUrl) || !string.IsNullOrEmpty(buildLogPath))
            {
                return await RunUpdateWithClassificationAsync(
                    commitSha, 
                    packagePath, 
                    apiViewUrl, 
                    buildLogPath, 
                    ct);
            }

            // Otherwise, run standard update flow without classification
            // Package path is required for standard flow
            if (string.IsNullOrEmpty(packagePath) || !Directory.Exists(packagePath))
            {
                return new CustomizedCodeUpdateResponse 
                { 
                    Message = $"Package path is required and must exist: {packagePath ?? "<not provided>"}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput, 
                    ResponseError = $"Package path does not exist: {packagePath}" 
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

            // Standard update flow without classification
            return await RunStandardUpdateAsync(commitSha, packagePath, languageService, ct);
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
    /// Runs the update workflow with feedback classification and retry loop.
    /// </summary>
    private async Task<CustomizedCodeUpdateResponse> RunUpdateWithClassificationAsync(
        string commitSha,
        string? packagePath,
        string? apiViewUrl,
        string? buildLogPath,
        CancellationToken ct)
    {
        // Step 1: Extract feedback from provided sources
        IFeedbackInput feedbackInput;
        
        if (!string.IsNullOrEmpty(apiViewUrl))
        {
            var feedbackInputLogger = loggerFactory.CreateLogger<APIViewFeedbackInput>();
            feedbackInput = new APIViewFeedbackInput(apiViewUrl, feedbackHelper, feedbackInputLogger);
        }
        else if (!string.IsNullOrEmpty(buildLogPath))
        {
            // Read build log content
            if (!File.Exists(buildLogPath))
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Build log file not found: {buildLogPath}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                    ResponseError = $"Build log file not found: {buildLogPath}"
                };
            }
            var buildLogContent = await File.ReadAllTextAsync(buildLogPath, ct);
            var feedbackInputLogger = loggerFactory.CreateLogger<BuildErrorFeedbackInput>();
            feedbackInput = new BuildErrorFeedbackInput(buildLogContent, feedbackInputLogger);
        }
        else
        {
            return new CustomizedCodeUpdateResponse
            {
                Message = "No feedback source provided",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                ResponseError = "Either --apiview-url or --build-log must be provided"
            };
        }

        var feedbackContext = await feedbackInput.PreprocessAsync(ct);
        
        // Auto-discover package path if not provided
        if (string.IsNullOrEmpty(packagePath))
        {
            packagePath = await DiscoverPackagePathAsync(feedbackContext.Language, feedbackContext.PackageName, ct);
            if (string.IsNullOrEmpty(packagePath))
            {
                logger.LogWarning("Could not discover package path for {Language}/{PackageName}. Proceeding with classification only.", 
                    feedbackContext.Language, feedbackContext.PackageName);
            }
        }
        
        // Get language service after package path is determined (if we have a path)
        LanguageService? languageService = null;
        if (!string.IsNullOrEmpty(packagePath))
        {
            languageService = await GetLanguageServiceAsync(packagePath, ct);
            if (!languageService.IsCustomizedCodeUpdateSupported)
            {
                logger.LogWarning("Language service does not support customized code update. Proceeding with classification only.");
                languageService = null;
            }
        }
        
        var language = languageService?.Language.ToString() ?? feedbackContext.Language ?? "Unknown";
        var context = new OrchestrationContext(feedbackContext.FormattedFeedback, language);

        // Step 2: Classify each feedback item individually
        logger.LogInformation("Classifying {Count} feedback items individually", feedbackContext.FeedbackItems.Count);
        var itemClassifications = new List<(FeedbackItem Item, FeedbackClassifier.ClassificationResult Result)>();
        
        foreach (var item in feedbackContext.FeedbackItems)
        {
            logger.LogInformation("Classifying item: {Id}", item.Id);
            var itemContext = new OrchestrationContext(item.FormattedForPrompt, language);
            var itemResult = await feedbackClassifier.ClassifyAsync(itemContext, ct);
            itemClassifications.Add((item, itemResult));
            
            // Output individual classification
            logger.LogInformation("--- Item {Id} Classification ---", item.Id);
            logger.LogInformation("Classification: {Classification}", itemResult.Classification);
            logger.LogInformation("Reason: {Reason}", itemResult.Reason);
            if (!string.IsNullOrEmpty(itemResult.NextAction))
            {
                logger.LogInformation("Next Action: {NextAction}", itemResult.NextAction);
            }
            logger.LogInformation("--------------------------------");
        }

        // Step 3: Aggregate classifications to determine overall action
        var overallClassification = AggregateClassifications(itemClassifications);
        
        logger.LogInformation("=== Overall Classification Result ===");
        logger.LogInformation("Classification: {Classification}", overallClassification.Classification);
        logger.LogInformation("Reason: {Reason}", overallClassification.Reason);
        if (!string.IsNullOrEmpty(overallClassification.NextAction))
        {
            logger.LogInformation("Combined Next Actions:\n{NextAction}", overallClassification.NextAction);
        }
        logger.LogInformation("====================================");

        // Step 4: Handle based on overall classification
        if (overallClassification.Classification == "SUCCESS")
        {
            return new CustomizedCodeUpdateResponse
            {
                Message = $"All feedback items resolved. {overallClassification.Reason}",
                NextSteps = SuccessPatchesAppliedNextSteps.ToList(),
                Classifications = itemClassifications.Select(ic => new CustomizedCodeUpdateResponse.ItemClassificationDetails
                {
                    ItemId = ic.Item.Id,
                    Classification = ic.Result.Classification,
                    Reason = ic.Result.Reason,
                    NextAction = ic.Result.NextAction
                }).ToList()
            };
        }

        if (overallClassification.Classification == "FAILURE")
        {
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Customization requires manual intervention. {overallClassification.Reason}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                ResponseError = overallClassification.Reason,
                NextSteps = new List<string> { overallClassification.NextAction ?? "No guidance available" },
                Classifications = itemClassifications.Select(ic => new CustomizedCodeUpdateResponse.ItemClassificationDetails
                {
                    ItemId = ic.Item.Id,
                    Classification = ic.Result.Classification,
                    Reason = ic.Result.Reason,
                    NextAction = ic.Result.NextAction
                }).ToList()
            };
        }

        // Step 5: For PHASE_A items, check if we can proceed with updates
        if (languageService == null || string.IsNullOrEmpty(packagePath))
        {
            return new CustomizedCodeUpdateResponse
            {
                Message = $"Cannot proceed with TypeSpec updates - no valid package path or language service. {itemClassifications.Count} items classified.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                ResponseError = "Missing package path or language service",
                NextSteps = new List<string> { overallClassification.NextAction ?? "Please provide --package-path explicitly" },
                Classifications = itemClassifications.Select(ic => new CustomizedCodeUpdateResponse.ItemClassificationDetails
                {
                    ItemId = ic.Item.Id,
                    Classification = ic.Result.Classification,
                    Reason = ic.Result.Reason,
                    NextAction = ic.Result.NextAction
                }).ToList()
            };
        }

        // Step 6: Retry loop for PHASE_A items (TypeSpec changes needed)
        for (int iteration = 1; iteration <= FeedbackClassifier.MaxIterations; iteration++)
        {
            context.Iteration = iteration;
            logger.LogInformation("Starting iteration {Iteration}/{Max}", iteration, FeedbackClassifier.MaxIterations);

            // Classify feedback
            var classification = await feedbackClassifier.ClassifyAsync(context, ct);
            
            // Output classification results to console
            logger.LogInformation("=== Classification Result ===");
            logger.LogInformation("Classification: {Classification}", classification.Classification);
            logger.LogInformation("Reason: {Reason}", classification.Reason);
            if (!string.IsNullOrEmpty(classification.NextAction))
            {
                logger.LogInformation("Next Action:\n{NextAction}", classification.NextAction);
            }
            logger.LogInformation("============================");

            logger.LogInformation("============================");

            // Handle SUCCESS or explicit FAILURE
            if (classification.Classification == "SUCCESS")
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Customization succeeded. {classification.Reason}",
                    NextSteps = SuccessPatchesAppliedNextSteps.ToList()
                };
            }

            if (classification.Classification == "FAILURE")
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Customization exhausted. {classification.Reason}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                    ResponseError = classification.Reason,
                    NextSteps = new List<string> { classification.NextAction ?? "No guidance available" }
                };
            }

            // Step 3: Attempt regeneration and patching (only if we have language service and package path)
            if (languageService == null || string.IsNullOrEmpty(packagePath))
            {
                return new CustomizedCodeUpdateResponse
                {
                    Message = $"Cannot proceed with update - no valid package path or language service. Classification: {classification.Classification}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                    ResponseError = "Missing package path or language service",
                    NextSteps = new List<string> { classification.NextAction ?? "Please provide --package-path explicitly" }
                };
            }
            
            var updateResult = await RunStandardUpdateAsync(commitSha, packagePath, languageService, ct);
            
            // Track results for next iteration
            if (updateResult.ErrorCode == null)
            {
                context.AddBuildSuccess();
                // Success on this iteration
                return updateResult;
            }
            else
            {
                context.AddBuildError(updateResult.ResponseError ?? "Unknown error");
                
                // If max iterations reached, return FAILURE with guidance
                if (iteration >= FeedbackClassifier.MaxIterations)
                {
                    // Final classification to get NextAction guidance
                    var finalClassification = await feedbackClassifier.ClassifyAsync(context, ct);
                    return new CustomizedCodeUpdateResponse
                    {
                        Message = $"Max iterations ({FeedbackClassifier.MaxIterations}) reached. {finalClassification.Reason}",
                        ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
                        ResponseError = updateResult.ResponseError,
                        NextSteps = new List<string> { finalClassification.NextAction ?? "Manual intervention required" }
                    };
                }
            }
        }

        // Should not reach here, but handle as safety
        return new CustomizedCodeUpdateResponse
        {
            Message = "Update process completed without resolution",
            ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
            ResponseError = "Retry loop exhausted"
        };
    }

    /// <summary>
    /// Runs the standard update flow without classification (original behavior).
    /// </summary>
    private async Task<CustomizedCodeUpdateResponse> RunStandardUpdateAsync(
        string commitSha,
        string packagePath,
        LanguageService languageService,
        CancellationToken ct)
    {
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

    /// <summary>
    /// Aggregates individual item classifications into an overall classification decision.
    /// Priority: FAILURE > PHASE_A > SUCCESS
    /// </summary>
    private FeedbackClassifier.ClassificationResult AggregateClassifications(
        List<(FeedbackItem Item, FeedbackClassifier.ClassificationResult Result)> itemClassifications)
    {
        var failureItems = itemClassifications.Where(ic => ic.Result.Classification == "FAILURE").ToList();
        var phaseAItems = itemClassifications.Where(ic => ic.Result.Classification == "PHASE_A").ToList();
        var successItems = itemClassifications.Where(ic => 
            ic.Result.Classification == "SUCCESS" || ic.Result.Classification == "KEEP_AS_IS").ToList();

        // If any item is FAILURE, overall is FAILURE
        if (failureItems.Any())
        {
            var failureReasons = string.Join("\n- ", failureItems.Select(f => $"Item {f.Item.Id}: {f.Result.Reason}"));
            var combinedActions = string.Join("\n\n", failureItems
                .Where(f => !string.IsNullOrEmpty(f.Result.NextAction))
                .Select(f => $"Item {f.Item.Id}:\n{f.Result.NextAction}"));

            return new FeedbackClassifier.ClassificationResult
            {
                Classification = "FAILURE",
                Reason = $"{failureItems.Count} item(s) require manual intervention:\n- {failureReasons}",
                Iteration = 1,
                NextAction = string.IsNullOrEmpty(combinedActions) ? null : combinedActions
            };
        }

        // If any item is PHASE_A, overall is PHASE_A
        if (phaseAItems.Any())
        {
            var phaseAReasons = string.Join("\n- ", phaseAItems.Select(p => $"Item {p.Item.Id}: {p.Result.Reason}"));
            var combinedActions = string.Join("\n\n", phaseAItems
                .Where(p => !string.IsNullOrEmpty(p.Result.NextAction))
                .Select(p => $"Item {p.Item.Id}:\n{p.Result.NextAction}"));

            return new FeedbackClassifier.ClassificationResult
            {
                Classification = "PHASE_A",
                Reason = $"{phaseAItems.Count} item(s) require TypeSpec changes:\n- {phaseAReasons}",
                Iteration = 1,
                NextAction = string.IsNullOrEmpty(combinedActions) ? null : combinedActions
            };
        }

        // All items are SUCCESS or KEEP_AS_IS
        var successReasons = string.Join("\n- ", successItems.Select(s => $"Item {s.Item.Id}: {s.Result.Reason}"));
        return new FeedbackClassifier.ClassificationResult
        {
            Classification = "SUCCESS",
            Reason = $"All {successItems.Count} item(s) resolved:\n- {successReasons}",
            Iteration = 1,
            NextAction = null
        };
    }
}
