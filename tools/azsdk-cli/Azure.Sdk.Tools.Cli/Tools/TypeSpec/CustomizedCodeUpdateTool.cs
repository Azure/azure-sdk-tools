// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Services.TypeSpec;
using Azure.Sdk.Tools.Cli.Tools.Core;
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
                Message = $"Invalid TypeSpec project path: {tspProjectPath}. Directory must exist and contain tspconfig.yaml.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"Invalid TypeSpec project path: {tspProjectPath}. Directory must exist and contain tspconfig.yaml."
            };
        }

        // Detect if customizationRequest is an APIView URL (prod or staging)
        string? apiViewUrl = IsApiViewUrl(customizationRequest) ? customizationRequest : null;

        // Get language info — prefer APIView metadata when a URL is provided, fall back to package path
        LanguageService languageService;
        if (apiViewUrl != null)
        {
            try
            {
                var language = await feedbackService.GetLanguageAsync(apiViewUrl, ct);
                var sdkLanguage = language != null ? SdkLanguageHelpers.GetSdkLanguage(language) : SdkLanguage.Unknown;
                languageService = sdkLanguage != SdkLanguage.Unknown
                    ? GetLanguageService(sdkLanguage)
                    : await GetLanguageServiceAsync(packagePath, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to detect language from APIView URL; falling back to package path detection.");
                languageService = await GetLanguageServiceAsync(packagePath, ct);
            }
        }
        else
        {
            languageService = await GetLanguageServiceAsync(packagePath, ct);
        }

        // Step 1: try tsp fixes
        var tries = 0;
        var maxTries = 2;
        bool buildSucceeded = false;
        string? buildError = null;

        List<FeedbackItem> feedbackItems;
        if (apiViewUrl != null)
        {
            try
            {
                feedbackItems = await GetFeedbackItems(apiViewUrl: apiViewUrl, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch feedback items from APIView URL.");
                return new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    Message = $"Failed to fetch feedback from APIView: {ex.Message}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError
                };
            }
        }
        else
        {
            feedbackItems = await GetFeedbackItems(plainTextFeedback: customizationRequest, ct: ct);
        }
        var feedbackDictionary = feedbackItems.ToDictionary(i => i.Id, i => i);

        List<string> changesMade = new();
        List<string> manualInterventions = new();
        StringBuilder classifierAnalysis = new();
        StringBuilder tspFixFailedReasons = new();
        do
        {
            // TODO - need to update this to avoid casting to/from list
            var response = await _classifierService.ClassifyItemsAsync([.. feedbackDictionary.Values], globalContext: string.Join(";", changesMade), tspProjectPath, ct: ct);

            if (response.Classifications == null || response.Classifications.Count == 0)
            {
                if (tries == 0)
                {
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = false,
                        Message = "Feedback could not be classified.",
                        ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                        BuildResult = "Feedback could not be classified."
                    };
                }

                // On subsequent iterations, no classifications means nothing more to fix via TSP
                logger.LogInformation("No further TSP-applicable classifications found on iteration {Iteration}.", tries + 1);
                break;
            }

            var tspFixFailed = 0;
            var tspFixSucceeded = 0;
            var tspApplicable = response.Classifications.Count(c => c.Classification == ClassificationTspApplicable);
            var codeCustomizations = response.Classifications.Count(c => c.Classification == ClassificationCodeCustomization);
            var manualChanges = response.Classifications.Count(c => c.Classification == ClassificationRequiresManualIntervention);
            var noChanges = response.Classifications.Count(c => c.Classification == ClassificationSuccess);

            logger.LogInformation("Classification summary: TSP_APPLICABLE={TspApplicable}, CODE_CUSTOMIZATION={CodeCustomization}, REQUIRES_MANUAL_INTERVENTION={Manual}, SUCCESS={Success}", tspApplicable, codeCustomizations, manualChanges, noChanges);

            tspApplicable = 0;
            codeCustomizations = 0;
            manualChanges = 0;
            noChanges = 0;

            foreach (var itemDetails in response.Classifications)
            {
                feedbackDictionary.TryGetValue(itemDetails.ItemId, out var feedbackItem);

                if (feedbackItem == null)
                {
                    logger.LogWarning("Classifier returned non-existent feedback item ID '{ItemId}', skipping.", itemDetails.ItemId);
                    continue;
                }

                feedbackItem?.AppendContext($"Iteration {tries+1}");

                // Accumulate classifier analysis for downstream patch agent
                classifierAnalysis.AppendLine($"[{itemDetails.ItemId}] Classification: {itemDetails.Classification}, Reason: {itemDetails.Reason}");

                if (itemDetails.Classification == ClassificationTspApplicable)
                {
                    tspApplicable++;
                    feedbackItem.AppendContext($"Iteration {tries+1}");
                    logger.LogDebug("Applying tsp customization for: {feedback}", itemDetails.Text);
                    var languageTaggedRequest = $"For {languageService.Language}: {itemDetails.Text}";
                    var tspCustomizationResult = await typeSpecCustomizationService.ApplyCustomizationAsync(tspProjectPath, languageTaggedRequest, ct: ct);

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

                    // Don't try and fix the same feedback again  
                    feedbackDictionary.Remove(itemDetails.ItemId);
                }
                else if (itemDetails.Classification == ClassificationRequiresManualIntervention)
                {
                    manualChanges++;
                    manualInterventions.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");

                    // Don't try and fix the same feedback again
                    feedbackDictionary.Remove(itemDetails.ItemId);
                }
                else if (itemDetails.Classification == ClassificationSuccess)
                {
                    noChanges++;

                    // Don't try and fix the same feedback again
                    feedbackDictionary.Remove(itemDetails.ItemId);
                }
            }

            // Exit cases for the first attempt
            if (tries == 0)
            {
                // Nothing was classified as tsp applicable and at least some feedback requires manual intervention
                if (tspApplicable == 0 && codeCustomizations == 0 && manualChanges > 0)
                {
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = true,
                        Message = "The requested changes require manual intervention and cannot be applied via TypeSpec customizations.",
                        NextSteps = manualInterventions
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

                // All tsp fixes failed to be applied - signaling a deeper error
                if (tspFixSucceeded == 0 && tspFixFailed > 0)
                {
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = false,
                        Message = $"Failed to apply any TypeSpec customizations: {tspFixFailedReasons}",
                        ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed
                    };
                }

                // All items are code customizations — no TSP changes were made, skip regen
                // but still build to get error context for the patch agent
                if (tspApplicable == 0 && codeCustomizations > 0)
                {
                    logger.LogInformation("All items classified as CODE_CUSTOMIZATION — skipping regen, building for error context.");
                    var (codeCustSuccess, codeCustError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
                    buildSucceeded = codeCustSuccess;
                    buildError = codeCustError;
                    break;
                }
            }

            // No more TSP-applicable items — remaining items need code customization, skip reclassification
            if (tspApplicable == 0 && codeCustomizations > 0)
            {
                logger.LogInformation("No TSP-applicable items remain; {CodeCustomizations} item(s) require code customization.", codeCustomizations);
                break;
            }

            // Don't waste time regenerating if no TSP fixes were successfully applied
            if (tspFixSucceeded > 0)
            {
                logger.LogDebug("Regenerating {packagePath}", packagePath);
                // Regenerate SDK using local spec repo
                var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, localSpecRepoPath: tspProjectPath, isCli: false, ct: ct);
                if (!regenResult.IsSuccessful)
                {
                    logger.LogWarning("Regeneration failed: {Error}", regenResult.ResponseError);

                    // Append regen failure context so the classifier can re-classify for manual intervention
                    var regenContext = $"Regeneration failed: {regenResult.ResponseError}";
                    foreach (var item in feedbackDictionary.Values)
                    {
                        item.AppendContext(regenContext, "Regeneration Result");
                    }

                    buildSucceeded = false;
                    buildError = regenContext;
                    tries++;
                    continue;
                }
            }

            logger.LogDebug("Building {packagePath}", packagePath);
            var (success, error, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

            buildSucceeded = success;
            buildError = error;

            // Append build result context to all remaining feedback items for the next iteration
            if (tries + 1 < maxTries)
            {
                var buildContext = success ? "Build succeeded." : (error ?? "Build failed with unknown error.");
                foreach (var item in feedbackDictionary.Values)
                {
                    item.AppendContext(buildContext, "Build Result");
                }
            }

            tries++;
        } while (!buildSucceeded && tries < maxTries);

        if (buildSucceeded)
        {
            logger.LogInformation("Build passed after Typespec customizations.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed after attempting TypeSpec customizations.",
                TypeSpecChangesSummary = changesMade,
                NextSteps = manualInterventions,
            };
        }

        // Step 2: If the build failed, start customized code update process

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

        // Step 3: Check for customization files to repair
        var customizationRoot = languageService.HasCustomizations(packagePath, ct);
        if (customizationRoot == null)
        {
            logger.LogInformation("Build failed but no customization files found.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
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

        foreach (var patch in patches)
        {
            var patchDetail = patch.Description.StartsWith($"Patch applied to {patch.FilePath}: ")
                ? patch.Description[$"Patch applied to {patch.FilePath}: ".Length..]
                : patch.Description;
            logger.LogInformation("Patch applied: {File} — {Detail}", patch.FilePath, patchDetail);
        }

        if (patches.Count == 0)
        {
            logger.LogInformation("No patches applied.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "No patches could be applied - automated repair found nothing to fix.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed,
                BuildResult = buildError
            };
        }

        // Step 5: Regenerate if Java (only Java needs regen after patching customization files)
        if (languageService.Language == SdkLanguage.Java)
        {
            logger.LogInformation("Regenerating code after patches (Java)...");
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha: null, isCli: false, localSpecRepoPath: tspProjectPath, ct);
            if (!regenResult.IsSuccessful)
            {
                logger.LogWarning("Regeneration failed: {Error}", regenResult.ResponseError);
                return new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    Message = $"Regeneration failed after patches: {regenResult.ResponseError}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed,
                    BuildResult = regenResult.ResponseError,
                    TypeSpecChangesSummary = changesMade,
                    AppliedPatches = patches
                };
            }
        }

        // Step 6: Final build to validate
        logger.LogInformation("Running final build to validate...");
        var (finalBuildSuccess, finalBuildError, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

        if (finalBuildSuccess)
        {
            logger.LogInformation("Build passed after repairs.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed after repairs.",
                TypeSpecChangesSummary = changesMade,
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
            TypeSpecChangesSummary = changesMade,
            AppliedPatches = patches
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
    /// Returns <see langword="true"/> if <paramref name="value"/> is an absolute HTTP/HTTPS URL
    /// whose host matches a known APIView environment (production or staging).
    /// Recognised hosts: <c>apiview.dev</c>, <c>apiview.org</c>, <c>apiviewstagingtest.com</c>.
    /// </summary>
    internal static bool IsApiViewUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return false; }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) { return false; }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) { return false; }
        var host = uri.Host;
        return host.EndsWith("apiview.dev", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("apiview.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase);
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
