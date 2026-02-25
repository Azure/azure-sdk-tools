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

/// <summary>
/// MCP tool that updates SDK code from TypeSpec, applies patches to customization files,
/// regenerates code, builds, and provides intelligent analysis and recommendations for updating customization code.
/// </summary>
[McpServerToolType, Description("Updates SDK code from TypeSpec, applies patches to customization files, regenerates code, builds, provides intelligent analysis and recommendations for updating customization code.")]
public class CustomizedCodeUpdateTool : LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly ITypeSpecHelper typeSpecHelper;
    private readonly IAPIViewFeedbackService feedbackService;
    private readonly ILoggerFactory loggerFactory;
    private readonly ICopilotAgentRunner _agentRunner;

    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";
    private const int CommandTimeoutInMinutes = 30;
    
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tspClientHelper"/> is null.</exception>
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
        this.tspClientHelper = tspClientHelper ?? throw new ArgumentNullException(nameof(tspClientHelper));
        this.typeSpecHelper = typeSpecHelper;
        this.feedbackService = feedbackService;
        this.loggerFactory = loggerFactory;
        _agentRunner = agentRunner;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    protected override Command GetCommand() =>
        new McpCommand("customized-update", "Update customized TypeSpec-generated client code, apply patches, regenerate, build, return result.", CustomizedCodeUpdateToolName)
        {
            SharedOptions.PackagePath,
        };

    /// <inheritdoc />
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath, nameof(packagePath));
        try
        {
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
    {
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

    /// <summary>
    /// Classifies feedback items from various sources (APIView, build errors, etc.) as TSP_APPLICABLE
    /// (can be resolved with TSP customizations), SUCCESS (no action needed), or REQUIRES_MANUAL_INTERVENTION (requires manual code customizations).
    /// </summary>
    private async Task<FeedbackClassificationResponse> Classify(
        string tspProjectPath,
        string? apiViewUrl,
        string? plainTextFeedback,
        string? plainTextFeedbackFile,
        string? language,
        string? serviceName,
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
                feedbackBatch = await new APIViewFeedbackSource(apiViewUrl, feedbackService, loggerFactory.CreateLogger<APIViewFeedbackSource>()).CreateBatchAsync(ct);
            }
            else if (!string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                feedbackBatch = await new PlainTextFeedbackSource(plainTextFeedback, loggerFactory.CreateLogger<PlainTextFeedbackSource>()).CreateBatchAsync(ct);
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

            var resolvedLanguage = language ?? feedbackBatch.Language;
            if (string.IsNullOrEmpty(resolvedLanguage))
            {
                throw new ArgumentException("Language is required but could not be determined from the feedback source. Please specify --language (e.g. python, java, dotnet, go, javascript).");
            }

            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentException("Service name is required. Please specify --service-name.");
            }

            return await classifier.ClassifyItemsAsync(feedbackBatch.Items, globalContext: "", resolvedLanguage, serviceName, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Classification failed");
            throw;
        }
    }
}
