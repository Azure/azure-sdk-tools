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
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly FeedbackClassifier feedbackClassifier;
    private readonly IAPIViewFeedbackCustomizationsHelpers feedbackHelper;
    private readonly ILoggerFactory loggerFactory;
    private readonly IMicroagentHostService _microagentHost;
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
        FeedbackClassifier feedbackClassifier,
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

        // Step 3: Return classifications for all items
        logger.LogInformation("=== Classification Summary ===");
        logger.LogInformation("Classified {Count} feedback items", itemClassifications.Count);
        foreach (var (item, result) in itemClassifications)
        {
            logger.LogInformation("- {ItemId}: {Classification}", item.Id, result.Classification);
        }
        logger.LogInformation("============================");

        // Step 4: For FAILURE items with NextAction, enhance guidance with detailed agent response
        var enhancedClassifications = new List<CustomizedCodeUpdateResponse.ItemClassificationDetails>();
        foreach (var (item, result) in itemClassifications)
        {
            if (result.Classification == "FAILURE" && !string.IsNullOrEmpty(result.NextAction))
            {
                logger.LogInformation("Enhancing guidance for failed item: {ItemId}", item.Id);
                var enhancedGuidance = await EnhanceFailureGuidanceAsync(item, result, language, ct);
                enhancedClassifications.Add(new CustomizedCodeUpdateResponse.ItemClassificationDetails
                {
                    ItemId = item.Id,
                    Classification = result.Classification,
                    Reason = result.Reason,
                    NextAction = enhancedGuidance
                });
            }
            else
            {
                enhancedClassifications.Add(new CustomizedCodeUpdateResponse.ItemClassificationDetails
                {
                    ItemId = item.Id,
                    Classification = result.Classification,
                    Reason = result.Reason,
                    NextAction = result.NextAction
                });
            }
        }

        // Return response with all individual classifications
        return new CustomizedCodeUpdateResponse
        {
            Message = $"Classified {itemClassifications.Count} feedback item(s)",
            Classifications = enhancedClassifications
        };
    }

    /// <summary>
    /// Enhances failure guidance by calling the agent with error context and next action.
    /// </summary>
    private async Task<string> EnhanceFailureGuidanceAsync(
        FeedbackItem item,
        FeedbackClassifier.ClassificationResult result,
        string language,
        CancellationToken ct)
    {
        try
        {
            var prompt = $@"You are helping a developer fix an issue that cannot be resolved with TypeSpec changes alone.

**Error Context:**
{item.FormattedForPrompt}

**Initial Classification:**
- Reason: {result.Reason}
- Suggested Action: {result.NextAction}

**Your Task:**
Provide detailed, actionable guidance for the {language} SDK to resolve this issue. Include:
1. Specific files or locations to modify
2. Code examples or patterns to follow
3. Step-by-step instructions
4. Any language-specific considerations for {language}

Be concrete and specific. Avoid generic advice.";

            var enhancedGuidance = await _microagentHost.RunAgentToCompletion(new Microagent<string>
            {
                Instructions = prompt,
                Model = _model,
                MaxToolCalls = 0 // No tools needed for this enhancement
            }, ct);

            return enhancedGuidance ?? result.NextAction;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enhance guidance for item {ItemId}, using original NextAction", item.Id);
            return result.NextAction ?? "Manual intervention required";
        }
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
}
