// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

[McpServerToolType, Description("Update customized SDK code after TypeSpec regeneration: creates a new generation, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool: LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly ITypeSpecHelper typeSpecHelper;
    private readonly IAPIViewFeedbackService feedbackService;
    private readonly ILoggerFactory loggerFactory;
    private readonly ICopilotAgentRunner _agentRunner;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    /// <param name="typeSpecHelper">The TypeSpec helper for spec repo path resolution.</param>
    /// <param name="feedbackService">The feedback service for extracting feedback from various sources.</param>
    /// <param name="loggerFactory">The logger factory for creating loggers.</param>
    /// <param name="agentRunner">The copilot agent runner for LLM-powered classification.</param>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        ITypeSpecHelper typeSpecHelper,
        IAPIViewFeedbackService feedbackService,
        ILoggerFactory loggerFactory,
        ICopilotAgentRunner agentRunner
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper;
        this.typeSpecHelper = typeSpecHelper;
        this.feedbackService = feedbackService;
        this.loggerFactory = loggerFactory;
        _agentRunner = agentRunner;
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
            var (regenSuccess, regenError) = await RegenerateAfterPatchesAsync(packagePath, commitSha, ct);
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
    /// Classifies feedback items from various sources (APIView, build errors, etc.) as TSP_APPLICABLE, SUCCESS, or FAILURE.
    /// </summary>
    private async Task<FeedbackClassificationResponse> Classify(
        string tspProjectPath,
        string? apiViewUrl,
        string? plainTextFeedback,
        string? plainTextFeedbackFile,
        string? language,
        CancellationToken ct)
    {
        try
        {
            // Read feedback from file if provided
            if (!string.IsNullOrWhiteSpace(plainTextFeedbackFile))
            {
                if (!File.Exists(plainTextFeedbackFile))
                {
                    throw new FileNotFoundException($"Plain text feedback file does not exist: {plainTextFeedbackFile}");
                }

                plainTextFeedback = await File.ReadAllTextAsync(plainTextFeedbackFile, ct);
                logger.LogInformation("Read {length} characters from feedback file: {file}", plainTextFeedback.Length, plainTextFeedbackFile);
            }

            if (string.IsNullOrEmpty(tspProjectPath) || !Directory.Exists(tspProjectPath))
            {
                throw new DirectoryNotFoundException($"TypeSpec project path does not exist: {tspProjectPath}");
            }

            var classifier = new FeedbackClassifierService(
                _agentRunner,
                loggerFactory,
                typeSpecHelper,
                tspProjectPath
            );

            FeedbackBatch feedbackBatch;
            if (!string.IsNullOrWhiteSpace(apiViewUrl))
            {
                feedbackBatch = await new APIViewFeedbackItem(apiViewUrl, feedbackService, loggerFactory.CreateLogger<APIViewFeedbackItem>()).PreprocessAsync(ct);
            }
            else if (!string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                feedbackBatch = await new PlainTextFeedbackItem(plainTextFeedback, loggerFactory.CreateLogger<PlainTextFeedbackItem>()).PreprocessAsync(ct);
            }
            else
            {
                throw new ArgumentException("Either --apiview-url or --plain-text-feedback (or --plain-text-feedback-file) must be provided.");
            }

            if (feedbackBatch.Items.Count == 0)
            {
                return new FeedbackClassificationResponse
                {
                    Message = "No valid feedback items found.",
                    Classifications = []
                };
            }

            language ??= feedbackBatch.Language;
            if (string.IsNullOrEmpty(language))
            {
                throw new ArgumentException("Language is required but could not be determined from the feedback source. Please specify --language (e.g. python, java, dotnet, go, javascript).");
            }

            return await classifier.ClassifyItemsAsync(feedbackBatch.Items, globalContext: "", language, serviceName: null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Classification failed");
            throw;
        }
    }

    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Update customized TypeSpec-generated client code")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string commitSha, string packagePath, CancellationToken ct = default)
        => RunUpdateAsync(commitSha, packagePath, ct);

    private async Task<(bool Success, string? ErrorMessage)> RegenerateAfterPatchesAsync(string packagePath, string commitSha, CancellationToken ct)
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

}
