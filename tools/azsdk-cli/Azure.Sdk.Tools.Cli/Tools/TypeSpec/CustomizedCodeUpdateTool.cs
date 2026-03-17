// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.Core;
using Microsoft.Graph.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.TypeSpec;

/// <summary>
/// MCP tool that updates SDK code from TypeSpec, applies patches to customization files,
/// regenerates code, builds, and provides intelligent analysis and recommendations for updating customization code.
/// </summary>
[McpServerToolType, Description("Apply TypeSpec and SDK code customizations: updates client TypeSpec or SDK code, provides code update recommendations, and regenerates SDK packages.")]
public class CustomizedCodeUpdateTool : LanguageMcpTool
{
    private readonly ITspClientHelper tspClientHelper;
    private readonly IAPIViewFeedbackService feedbackService;
    private readonly IFeedbackClassifierService _classifierService;
    private readonly ITypeSpecCustomizationService typeSpecCustomizationService;
    private readonly ITypeSpecHelper typeSpecHelper;

    private const string CustomizedCodeUpdateToolName = "azsdk_customized_code_update";
    private const int CommandTimeoutInMinutes = 30;

    // Classification categories returned by the classifier
    private const string ClassificationTspApplicable = "TSP_APPLICABLE";
    private const string ClassificationCodeCustomization = "CODE_CUSTOMIZATION";
    private const string ClassificationRequiresManualIntervention = "REQUIRES_MANUAL_INTERVENTION";
    private const string ClassificationSuccess = "SUCCESS";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomizedCodeUpdateTool"/> class.
    /// </summary>
    /// <param name="logger">The logger for this tool.</param>
    /// <param name="languageServices">The collection of available language services.</param>
    /// <param name="gitHelper">The Git helper for repository operations.</param>
    /// <param name="tspClientHelper">The TypeSpec client helper for regeneration operations.</param>
    /// <param name="feedbackService">The feedback service for extracting feedback from various sources.</param>
    /// <param name="classifierService">The feedback classifier service for LLM-powered classification.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tspClientHelper"/> is null.</exception>
    public CustomizedCodeUpdateTool(
        ILogger<CustomizedCodeUpdateTool> logger,
        IEnumerable<LanguageService> languageServices,
        IGitHelper gitHelper,
        ITspClientHelper tspClientHelper,
        IAPIViewFeedbackService feedbackService,
        IFeedbackClassifierService classifierService,
        ITypeSpecCustomizationService typeSpecCustomizationService,
        ITypeSpecHelper typeSpecHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper ?? throw new ArgumentNullException(nameof(tspClientHelper));
        this.feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
        _classifierService = classifierService ?? throw new ArgumentNullException(nameof(classifierService));
        this.typeSpecCustomizationService = typeSpecCustomizationService ?? throw new ArgumentNullException(nameof(typeSpecCustomizationService));
        this.typeSpecHelper = typeSpecHelper ?? throw new ArgumentNullException(nameof(typeSpecHelper));
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Option<string> customizationRequestOption = new("--customization-request")
    {
        Description = "Description of the requested customization to apply to the TypeSpec.",
        Arity = ArgumentArity.ExactlyOne,
        Required = true
    };

    private readonly Option<string> typespecProjectPath = new("--tsp-project-path")
    {
        Description = "Absolute path to the local TypeSpec project directory (containing main.tsp/client.tsp) where customizations will be applied.",
        Arity = ArgumentArity.ExactlyOne,
        Required = true
    };

    protected override Command GetCommand() =>
        new McpCommand("customized-update", "Apply TypeSpec and SDK code customizations with AI-assisted analysis.", CustomizedCodeUpdateToolName)
        {
            SharedOptions.PackagePath,
            typespecProjectPath,
            customizationRequestOption,
        };

    /// <inheritdoc />
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packagePath = parseResult.GetValue(SharedOptions.PackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath, nameof(packagePath));

        var tspProjectPath = parseResult.GetValue(typespecProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tspProjectPath, nameof(tspProjectPath));

        var customizationRequest = parseResult.GetValue(customizationRequestOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(customizationRequest, nameof(customizationRequest));
        try
        {
            logger.LogInformation("Starting customized code update for {PackagePath}", packagePath);
            return await RunUpdateAsync(packagePath, tspProjectPath, customizationRequest, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customized code update failed");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Customized code update failed: {ex.Message}",
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
    /// <param name="tspProjectPath">Absolute path to the local TypeSpec project directory.</param>
    /// <param name="customizationRequest">Description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> indicating the outcome.</returns>
    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Applies patches to customization files based on build errors, regenerates code if needed (Java), builds, and returns success/failure with build result.")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string packagePath, string tspProjectPath, string customizationRequest, CancellationToken ct = default)
        => RunUpdateAsync(packagePath, tspProjectPath, customizationRequest, ct);

    /// <summary>
    /// Executes the update pipeline: classify → patch customizations → regen → build.
    /// </summary>
    /// <param name="packagePath">Absolute path to the SDK package directory.</param>
    /// <param name="tspProjectPath">Absolute path to the local TypeSpec project directory.</param>
    /// <param name="customizationRequest">Description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> with the pipeline result.</returns>
    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string packagePath, string tspProjectPath, string customizationRequest, CancellationToken ct)
    {
        // Validate input
        if (!Directory.Exists(packagePath))
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Package path does not exist: {packagePath}",
                Message = $"Package path does not exist: {packagePath}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"Package path does not exist: {packagePath}"
            };
        }

        if (!Directory.Exists(tspProjectPath))
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"TypeSpec project path does not exist: {tspProjectPath}",
                Message = $"TypeSpec project path does not exist: {tspProjectPath}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"TypeSpec project path does not exist: {tspProjectPath}"
            };
        }

        if (!typeSpecHelper.IsValidTypeSpecProjectPath(tspProjectPath))
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Invalid TypeSpec project path: {tspProjectPath}. Directory must exist and contain tspconfig.yaml.",
                Message = $"Invalid TypeSpec project path: {tspProjectPath}. Directory must exist and contain tspconfig.yaml.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"Invalid TypeSpec project path: {tspProjectPath}. Directory must exist and contain tspconfig.yaml."
            };
        }

        // Get language info
        var languageService = await GetLanguageServiceAsync(packagePath, ct);
        // TODO - do this once we add API view option
        // var language = await feedbackService.GetLanguageAsync(apiViewUrl, ct);

        var feedbackItems = await GetFeedbackItems(plainTextFeedback: customizationRequest, ct: ct);
        if (feedbackItems.Count == 0)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = "No feedback items provided. Please supply a customization request or API review URL.",
                Message = "No feedback items provided. Please supply a customization request or API review URL.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = "No feedback items to process."
            };
        }
        var feedbackDictionary = feedbackItems.ToDictionary(i => i.Id, i => i);

        List<string> changesMade = new();
        List<string> manualInterventions = new();
        StringBuilder classifierAnalysis = new();
        StringBuilder tspFixFailedReasons = new();
        bool buildSucceeded = false;
        string? buildError = null;

        // ── Pass 1: Classify feedback and apply TSP fixes ──
        // TODO - need to update this to avoid casting to/from list
        var response = await _classifierService.ClassifyItemsAsync([.. feedbackDictionary.Values], globalContext: string.Join(";", changesMade), tspProjectPath, ct: ct);

        if (response.Classifications == null || response.Classifications.Count == 0)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = "Feedback could not be classified.",
                Message = "Feedback could not be classified.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = "Feedback could not be classified."
            };
        }

        var tspFixFailed = 0;
        var tspFixSucceeded = 0;
        var tspApplicable = 0;
        var codeCustomizations = 0;
        var manualChanges = 0;
        var noChanges = 0;

        foreach (var itemDetails in response.Classifications)
        {
            feedbackDictionary.TryGetValue(itemDetails.ItemId, out var feedbackItem);

            if (feedbackItem == null)
            {
                logger.LogWarning("Classifier returned non-existent feedback item ID '{ItemId}', skipping.", itemDetails.ItemId);
                continue;
            }

            if (itemDetails.Classification == ClassificationTspApplicable)
            {
                tspApplicable++;
                logger.LogDebug("Applying tsp customization for: {feedback}", itemDetails.Text);
                var tspCustomizationResult = await typeSpecCustomizationService.ApplyCustomizationAsync(tspProjectPath, itemDetails.Text, ct: ct);

                if (tspCustomizationResult.Success)
                {
                    var changes = string.Join("; ", tspCustomizationResult.ChangesSummary);
                    logger.LogInformation("Successfully applied tsp customization changes, changes applied: {changes}", changes);
                    feedbackItem.AppendContext(changes, "Typespec changes applied");
                    changesMade.AddRange(tspCustomizationResult.ChangesSummary);
                    tspFixSucceeded++;
                }
                else
                {
                    logger.LogWarning("Some customizations failed to apply: {FailureReasons}", tspCustomizationResult.FailureReason);
                    feedbackItem.AppendContext(tspCustomizationResult.FailureReason ?? "Unknown failure", "TypeSpec customization failed");
                    tspFixFailedReasons.Append(tspCustomizationResult.FailureReason);
                    tspFixFailedReasons.Append("; ");
                    tspFixFailed++;
                }
            }
            else if (itemDetails.Classification == ClassificationCodeCustomization)
            {
                codeCustomizations++;
                logger.LogInformation("Item '{ItemId}' classified as CODE_CUSTOMIZATION — will be handled via code patching.", itemDetails.ItemId);
                classifierAnalysis.AppendLine($"[{itemDetails.ItemId}] Classification: {itemDetails.Classification}, Reason: {itemDetails.Reason}");
                feedbackDictionary.Remove(itemDetails.ItemId);
            }
            else if (itemDetails.Classification == ClassificationRequiresManualIntervention)
            {
                manualChanges++;
                manualInterventions.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                feedbackDictionary.Remove(itemDetails.ItemId);
            }
            else if (itemDetails.Classification == ClassificationSuccess)
            {
                noChanges++;
                feedbackDictionary.Remove(itemDetails.ItemId);
            }
        }

        // ── Early exit cases based on first classification ──

        // Nothing was classified as tsp applicable and at least some feedback requires manual intervention
        if (tspApplicable == 0 && codeCustomizations == 0 && manualChanges > 0)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "The requested changes require manual intervention and cannot be applied via TypeSpec customizations.",
                NextSteps = manualInterventions,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired
            };
        }

        // Everything was classified as success
        if (tspApplicable == 0 && codeCustomizations == 0 && noChanges > 0)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "No changes needed — the requested customizations are already in place."
            };
        }

        // ── Regen + Build if TSP fixes were applied ──
        if (tspFixSucceeded > 0)
        {
            logger.LogDebug("Regenerating {packagePath}", packagePath);
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, localSpecRepoPath: tspProjectPath, isCli: false, ct: ct);
            if (!regenResult.IsSuccessful)
            {
                logger.LogWarning("Regeneration failed: {Error}", regenResult.ResponseError);
                // Enrich remaining items with regen failure context for the second classifier pass
                foreach (var item in feedbackDictionary.Values)
                {
                    item.AppendContext($"Regeneration failed: {regenResult.ResponseError}", "Regeneration Result");
                }
            }
            else
            {
                logger.LogDebug("Building {packagePath}", packagePath);
                var (success, error, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
                buildSucceeded = success;
                buildError = error;

                if (buildSucceeded && codeCustomizations == 0)
                {
                    logger.LogInformation("Build passed after TypeSpec customizations.");
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = true,
                        Message = "Build passed after attempting TypeSpec customizations.",
                        TypeSpecChangesSummary = changesMade,
                        NextSteps = manualInterventions,
                    };
                }

                // Enrich remaining items with build error context for the second classifier pass
                if (!buildSucceeded)
                {
                    foreach (var item in feedbackDictionary.Values)
                    {
                        item.AppendContext(error ?? "Build failed with unknown error.", "Build Result");
                    }
                }
            }
        }

        // ── Pass 2: Re-classify remaining items with regen/build context ──
        // Items that had TSP fixes applied but regen/build failed get re-evaluated.
        // The classifier can now reclassify them as CODE_CUSTOMIZATION or REQUIRES_MANUAL_INTERVENTION.
        if (feedbackDictionary.Count > 0)
        {
            var secondResponse = await _classifierService.ClassifyItemsAsync([.. feedbackDictionary.Values], globalContext: string.Join(";", changesMade), tspProjectPath, ct: ct);

            if (secondResponse.Classifications != null)
            {
                foreach (var itemDetails in secondResponse.Classifications)
                {
                    if (itemDetails.Classification == ClassificationCodeCustomization)
                    {
                        codeCustomizations++;
                        logger.LogInformation("Item '{ItemId}' reclassified as CODE_CUSTOMIZATION on second pass.", itemDetails.ItemId);
                        classifierAnalysis.AppendLine($"[{itemDetails.ItemId}] Classification: {itemDetails.Classification}, Reason: {itemDetails.Reason}");
                        feedbackDictionary.Remove(itemDetails.ItemId);
                    }
                    else if (itemDetails.Classification == ClassificationRequiresManualIntervention)
                    {
                        manualInterventions.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                        feedbackDictionary.Remove(itemDetails.ItemId);
                    }
                    else if (itemDetails.Classification == ClassificationSuccess)
                    {
                        feedbackDictionary.Remove(itemDetails.ItemId);
                    }
                }
            }
        }

        // Build for error context if no build happened yet (pure CODE_CUSTOMIZATION path or regen failed)
        if (!buildSucceeded && buildError == null)
        {
            logger.LogInformation("Building for error context...");
            var (s, e, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
            buildSucceeded = s;
            buildError = e;
        }

        if (buildSucceeded && codeCustomizations == 0)
        {
            logger.LogInformation("Build passed after TypeSpec customizations.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed after attempting TypeSpec customizations.",
                TypeSpecChangesSummary = changesMade,
                NextSteps = manualInterventions,
            };
        }

        // Step 2: If the build failed or CODE_CUSTOMIZATION items still need patching, start customized code update process

        if (!languageService.IsCustomizedCodeUpdateSupported)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = "Language service does not support customized code updates.",
                Message = "Language service does not support customized code updates.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService,
                BuildResult = "No language service available for this package type."
            };
        }

        // Step 3: Check for customization files to repair
        var customizationRoot = languageService.HasCustomizations(packagePath, ct);
        if (customizationRoot == null)
        {
            logger.LogInformation("Build failed but no customization files found.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = string.IsNullOrWhiteSpace(buildError)
                    ? "Build failed but no customization files found to repair."
                    : $"Build failed but no customization files found to repair.\n{buildError}",
                Message = "Build failed but no customization files found to repair.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                BuildResult = buildError
            };
        }

        // Step 4: Apply patches based on build errors
        var patchContext = BuildPatchContext(customizationRequest, classifierAnalysis, buildError);

        logger.LogInformation("Applying patches to fix build errors...");
        var patches = await languageService.ApplyPatchesAsync(
            customizationRoot,
            packagePath,
            patchContext,
            ct);

        if (patches.Count == 0)
        {
            logger.LogInformation("No patches applied.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = string.IsNullOrWhiteSpace(buildError)
                    ? "No patches could be applied - automated repair found nothing to fix."
                    : $"No patches could be applied - automated repair found nothing to fix.\n{buildError}",
                Message = "No patches could be applied - automated repair found nothing to fix.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                BuildResult = buildError
            };
        }

        // Step 5: Regenerate if Java (only Java needs regen after patching customization files)
        if (languageService.Language == SdkLanguage.Java)
        {
            logger.LogInformation("Regenerating code after patches (Java)...");
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, localSpecRepoPath: tspProjectPath, isCli: false, ct: ct);
            if (!regenResult.IsSuccessful)
            {
                logger.LogWarning("Regeneration failed: {Error}", regenResult.ResponseError);
                return new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    ResponseError = $"Regeneration failed after patches: {regenResult.ResponseError}",
                    Message = $"Regeneration failed after patches: {regenResult.ResponseError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                    BuildResult = regenResult.ResponseError,
                    TypeSpecChangesSummary = changesMade,
                    AppliedPatches = patches
                };
            }
        }

        // Step 6: Final build to validate patches
        logger.LogInformation("Running final build to validate code customization patches...");
        var (finalBuildSuccess, finalBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

        if (finalBuildSuccess)
        {
            logger.LogInformation("Build passed after code customization patches.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed after code customization patches.",
                TypeSpecChangesSummary = changesMade,
                AppliedPatches = patches,
                NextSteps = manualInterventions,
            };
        }

        // Build still failing after patches
        logger.LogInformation("Build still failing after code customization patches.");
        return new CustomizedCodeUpdateResponse
        {
            Success = false,
            ResponseError = string.IsNullOrWhiteSpace(finalBuildError)
                ? "Code customization patches applied but build still failing."
                : $"Code customization patches applied but build still failing.\n{finalBuildError}",
            Message = "Code customization patches applied but build still failing.",
            ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed,
            BuildResult = finalBuildError,
            TypeSpecChangesSummary = changesMade,
            AppliedPatches = patches,
            NextSteps = manualInterventions,
        };
    }

    /// <summary>
    /// Builds a formatted context string for the patch agent, combining the original request,
    /// classifier analysis, and build errors into labeled markdown sections.
    /// </summary>
    /// <param name="customizationRequest">The original user customization request text.</param>
    /// <param name="classifierAnalysis">Accumulated classifier analysis from all classification iterations.</param>
    /// <param name="buildError">The build error output, if any.</param>
    /// <returns>A formatted markdown string combining all available context sections.</returns>
    internal static string BuildPatchContext(string? customizationRequest, StringBuilder classifierAnalysis, string? buildError)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(customizationRequest))
        {
            sb.AppendLine("## Original Request");
            sb.AppendLine(customizationRequest);
            sb.AppendLine();
        }
        if (classifierAnalysis.Length > 0)
        {
            sb.AppendLine("## Classifier Analysis");
            sb.AppendLine(classifierAnalysis.ToString());
        }
        if (!string.IsNullOrWhiteSpace(buildError))
        {
            sb.AppendLine("## Build Errors");
            sb.AppendLine(buildError);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gathers feedback items from the provided sources: APIView URL, plain text feedback, or a file containing plain text feedback.
    /// </summary>
    /// <param name="apiViewUrl">Optional APIView URL to extract feedback from.</param>
    /// <param name="plainTextFeedback">Optional plain text feedback string.</param>
    /// <param name="plainTextFeedbackFile">Optional path to a file containing plain text feedback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of <see cref="FeedbackItem"/> instances extracted from the provided sources.</returns>
    private async Task<List<FeedbackItem>> GetFeedbackItems(
        string? apiViewUrl = default,
        string? plainTextFeedback = default,
        string? plainTextFeedbackFile = default,
        CancellationToken ct = default)
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

            List<FeedbackItem> feedbackItems = [];
            if (!string.IsNullOrWhiteSpace(apiViewUrl))
            {
                feedbackItems = await feedbackService.GetFeedbackItemsAsync(apiViewUrl, ct);
            }
            else if (!string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                feedbackItems = [new FeedbackItem { Text = plainTextFeedback, Context = string.Empty }];
            }

            return feedbackItems;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to gather feedback items");
            throw;
        }
    }
}
