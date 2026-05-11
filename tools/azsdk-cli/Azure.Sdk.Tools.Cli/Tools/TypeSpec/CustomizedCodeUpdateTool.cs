// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
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
    private readonly INpxHelper npxHelper;

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
        ITypeSpecHelper typeSpecHelper,
        INpxHelper npxHelper
    ) : base(languageServices, gitHelper, logger)
    {
        this.tspClientHelper = tspClientHelper ?? throw new ArgumentNullException(nameof(tspClientHelper));
        this.feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
        _classifierService = classifierService ?? throw new ArgumentNullException(nameof(classifierService));
        this.typeSpecCustomizationService = typeSpecCustomizationService ?? throw new ArgumentNullException(nameof(typeSpecCustomizationService));
        this.typeSpecHelper = typeSpecHelper ?? throw new ArgumentNullException(nameof(typeSpecHelper));
        this.npxHelper = npxHelper ?? throw new ArgumentNullException(nameof(npxHelper));
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

        // Detect if customizationRequest is an APIView URL (prod or staging)
        string? apiViewUrl = IsApiViewUrl(customizationRequest) ? customizationRequest : null;

        var languageService = await ResolveLanguageServiceAsync(packagePath, apiViewUrl, ct);

        List<FeedbackItem> feedbackItems = [];
        FeedbackClassificationResponse response;
        try
        {
            response = await _classifierService.ClassifyItemsAsync(
                feedbackItems,
                globalContext: string.Empty,
                tspProjectPath: tspProjectPath,
                apiViewUrl: apiViewUrl,
                plainTextFeedback: customizationRequest,
                language: languageService.Language.ToString(),
                ct: ct);
        }
        catch (CopilotCliUnavailableException ex)
        {
            logger.LogError(ex, "GitHub Copilot CLI is not available.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = ex.Message,
                Message = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
                BuildResult = ex.Message
            };
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for feedback classification.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = ex.Message,
                Message = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = ex.Message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Feedback classification failed unexpectedly.");
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Feedback classification failed: {ex.Message}",
                Message = $"Feedback classification failed: {ex.Message}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
                BuildResult = $"Feedback classification failed: {ex.Message}"
            };
        }
        var feedbackDictionary = feedbackItems.ToDictionary(i => i.Id, i => i);

        List<string> changesMade = new();
        List<string> manualInterventions = new();
        StringBuilder codeCustomizationLog = new();
        StringBuilder tspFixFailedReasons = new();
        bool buildSucceeded = false;
        string? buildError = null;

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
                codeCustomizationLog.AppendLine($"[{itemDetails.ItemId}] Classification: {itemDetails.Classification}, Reason: {itemDetails.Reason}");
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
                Success = false,
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
                // JavaScript: apply customization merge after regeneration
                await ApplyJavaScriptCustomizationAsync(languageService, packagePath, ct);

                logger.LogDebug("Building {packagePath}", packagePath);
                var (success, error, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);
                buildSucceeded = success;
                buildError = error;

                if (buildSucceeded && codeCustomizations == 0)
                {
                    logger.LogInformation("Build passed after TypeSpec customizations.");
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = manualInterventions.Count == 0,
                        Message = manualInterventions.Count == 0
                            ? "Build passed after attempting TypeSpec customizations."
                            : "Build passed after attempting TypeSpec customizations, but some items require manual intervention.",
                        TypeSpecChangesSummary = changesMade,
                        NextSteps = manualInterventions,
                        ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
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
            var secondResponse = await _classifierService.ClassifyItemsAsync([.. feedbackDictionary.Values], globalContext: string.Join(";", changesMade), tspProjectPath: tspProjectPath, language: languageService.Language.ToString(), ct: ct);

            if (secondResponse.Classifications != null)
            {
                foreach (var itemDetails in secondResponse.Classifications)
                {
                    if (itemDetails.Classification == ClassificationCodeCustomization)
                    {
                        codeCustomizations++;
                        logger.LogInformation("Item '{ItemId}' reclassified as CODE_CUSTOMIZATION on second pass.", itemDetails.ItemId);
                        codeCustomizationLog.AppendLine($"[{itemDetails.ItemId}] Classification: {itemDetails.Classification}, Reason: {itemDetails.Reason}");
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
                Success = manualInterventions.Count == 0,
                Message = manualInterventions.Count == 0
                    ? "Build passed after attempting TypeSpec customizations."
                    : "Build passed after attempting TypeSpec customizations, but some items require manual intervention.",
                TypeSpecChangesSummary = changesMade,
                NextSteps = manualInterventions,
                ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
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
        var patchContext = BuildPatchContext(customizationRequest, codeCustomizationLog, buildError);

        logger.LogInformation("Applying patches to fix build errors...");
        var patches = await languageService.ApplyPatchesAsync(
            customizationRoot,
            packagePath,
            patchContext,
            ct);

        foreach (var patch in patches)
        {
            logger.LogInformation("{Description}", patch.Description);
        }

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
                Success = manualInterventions.Count == 0,
                Message = manualInterventions.Count == 0
                    ? "Build passed after code customization patches."
                    : "Build passed after code customization patches, but some items require manual intervention.",
                TypeSpecChangesSummary = changesMade,
                AppliedPatches = patches,
                NextSteps = manualInterventions,
                ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
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
    /// Resolves the language service to use: prefers language detected from an APIView URL,
    /// falls back to detecting from the package path.
    /// </summary>
    private async Task<LanguageService> ResolveLanguageServiceAsync(string packagePath, string? apiViewUrl, CancellationToken ct)
    {
        if (apiViewUrl != null)
        {
            try
            {
                var language = await feedbackService.GetLanguageAsync(apiViewUrl, ct);
                var sdkLanguage = language != null ? SdkLanguageHelpers.GetSdkLanguage(language) : SdkLanguage.Unknown;
                if (sdkLanguage != SdkLanguage.Unknown)
                {
                    return GetLanguageService(sdkLanguage);
                }
                logger.LogWarning("Could not determine language from APIView URL; falling back to package path detection.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to detect language from APIView URL; falling back to package path detection.");
            }
        }

        logger.LogInformation("Detecting language from package path: {PackagePath}", packagePath);
        return await GetLanguageServiceAsync(packagePath, ct);
    }

    /// <summary>
    /// Builds a formatted context string for the patch agent, combining the original request,
    /// classifier analysis, and build errors into labeled markdown sections.
    /// </summary>
    /// <param name="customizationRequest">The original user customization request text.</param>
    /// <param name="codeCustomizationLog">Accumulated code customization classification log from all classification passes.</param>
    /// <param name="buildError">The build error output, if any.</param>
    /// <returns>A formatted markdown string combining all available context sections.</returns>
    internal static string BuildPatchContext(string? customizationRequest, StringBuilder codeCustomizationLog, string? buildError)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(customizationRequest))
        {
            sb.AppendLine("## Original Request");
            sb.AppendLine(customizationRequest);
            sb.AppendLine();
        }
        if (codeCustomizationLog.Length > 0)
        {
            sb.AppendLine("## Classifier Analysis");
            sb.AppendLine(codeCustomizationLog.ToString());
        }
        if (!string.IsNullOrWhiteSpace(buildError))
        {
            sb.AppendLine("## Build Errors");
            sb.AppendLine(buildError);
        }
        return sb.ToString();
    }

    /// <summary>
    /// For JavaScript packages with customizations (<c>generated/</c> folder), runs
    /// <c>npx dev-tool customization apply</c> to perform a 3-way merge of newly regenerated
    /// code with existing customizations in <c>src/</c>.
    /// </summary>
    private async Task ApplyJavaScriptCustomizationAsync(LanguageService languageService, string packagePath, CancellationToken ct)
    {
        if (languageService.Language != SdkLanguage.JavaScript)
        {
            return;
        }

        if (languageService.HasCustomizations(packagePath, ct) == null)
        {
            return;
        }

        // dev-tool customization apply merges regenerated code with src/ customizations.
        // If src/ doesn't exist, there's nothing to merge into.
        var srcDir = Path.Combine(packagePath, "src");
        if (!Directory.Exists(srcDir))
        {
            logger.LogDebug("No src/ directory found at {SrcDir}, skipping dev-tool customization apply", srcDir);
            return;
        }

        logger.LogInformation("Running dev-tool customization apply for JavaScript package...");
        var result = await npxHelper.Run(
            new NpxOptions(
                package: null,
                args: ["dev-tool", "customization", "apply"],
                workingDirectory: packagePath),
            ct);

        if (result.ExitCode != 0)
        {
            logger.LogError("dev-tool customization apply exited with code {ExitCode}: {Output}", result.ExitCode, result.Output);
            throw new InvalidOperationException($"dev-tool customization apply failed with exit code {result.ExitCode}: {result.Output}");
        }

        logger.LogInformation("dev-tool customization apply completed successfully.");
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is an absolute HTTP/HTTPS URL
    /// whose host matches a known APIView environment (production or staging).
    /// Recognised hosts: <c>apiview.dev</c>, <c>*.apiview.dev</c>, <c>apiview.org</c>, <c>*.apiview.org</c>, <c>apiviewstagingtest.com</c>, <c>*.apiviewstagingtest.com</c>.
    /// </summary>
    internal static bool IsApiViewUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return false; }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) { return false; }
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) { return false; }
        var host = uri.IdnHost;
        return host.Equals("apiview.dev", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".apiview.dev", StringComparison.OrdinalIgnoreCase)
            || host.Equals("apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".apiviewstagingtest.com", StringComparison.OrdinalIgnoreCase);
    }

}
