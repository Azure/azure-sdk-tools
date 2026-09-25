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
    private const string MissingTypeSpecProjectPathMessage = "TypeSpec project path is missing; cannot run regeneration.";

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
    /// <param name="typeSpecCustomizationService">The TypeSpec customization service for applying patches and regenerating code.</param>
    /// <param name="typeSpecHelper">The TypeSpec helper for project and path validations.</param>
    /// <param name="npxHelper">The NPX helper for running Node.js commands.</param>
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

    public static readonly Option<string> PackagePathOpt = new("--package-path", "-p")
    {
        Description = "Absolute path to the SDK package directory. Required for `CustomCode` and `All`; not required for `SpecInputs`",
        Required = false,
    };

    private readonly Option<string> typespecProjectPath = new("--tsp-project-path")
    {
        Description = "Absolute path to the local TypeSpec project directory (containing main.tsp/client.tsp) where " +
                      "customizations will be applied. Required when the edit scope includes spec inputs (SpecInputs/All). " +
                      "Optional for custom-code-only repair (editScope CustomCode): when omitted, regeneration resolves the " +
                      "spec from the pinned commit in the package's tsp-location.yaml, so no local spec checkout is needed.",
        Arity = ArgumentArity.ZeroOrOne,
        Required = false
    };

    // Design intent: when editScope is CustomCode, the tool may apply custom-code workarounds for issues
    // that could also be fixed via client.tsp. This is by design to unblock users who are already in the
    // SDK (language) repo with a failing build, NOT because custom code is the ideal fix. The preferred
    // ("shift-left") path remains client.tsp; spec-level items are surfaced via SpecChangeRequired so the
    // resulting tech debt is visible and measurable, never silent.
    private readonly Option<EditScope> editScopeOption = new("--edit-scope")
    {
        Description = "Which source categories the tool may edit (flags: All, CustomCode, SpecInputs; default All). " +
                      "CustomCode restricts edits to custom (non-generated) code; spec-level failures are reported as " +
                      "out of scope (errorCode 'SpecChangeRequired') rather than applied.",
        Required = false,
        DefaultValueFactory = _ => EditScope.All
    };

    private readonly Option<int> maxAttemptsOption = new("--max-attempts")
    {
        Description = "Maximum custom-code repair attempts in one retained conversation (1..10, default 1). Values above 1 require CustomCode scope.",
        DefaultValueFactory = _ => 1
    };

    protected override Command GetCommand() =>
        new McpCommand("customized-update", "Apply TypeSpec and SDK code customizations with AI-assisted analysis.", CustomizedCodeUpdateToolName)
        {
            PackagePathOpt,
            typespecProjectPath,
            customizationRequestOption,
            editScopeOption,
            maxAttemptsOption,
        };

    /// <inheritdoc />
    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var packagePath = parseResult.GetValue(PackagePathOpt);

        var tspProjectPath = parseResult.GetValue(typespecProjectPath);

        var customizationRequest = parseResult.GetValue(customizationRequestOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(customizationRequest, nameof(customizationRequest));

        var editScope = parseResult.GetValue(editScopeOption);
        try
        {
            logger.LogInformation("Starting customized code update for {PackagePath} (editScope: {EditScope})", packagePath, editScope);
            return await RunUpdateAsync(packagePath, tspProjectPath, customizationRequest, editScope, ct,
                parseResult.GetValue(maxAttemptsOption));
        }
        catch (OperationCanceledException)
        {
            throw;
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
    /// regenerates code if needed (C# and Java), builds, and returns success/failure with build result.
    /// </summary>
    /// <param name="customizationRequest">Description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="packagePath">Absolute path to the SDK package directory. Required for `CustomCode` and `All`; not required for `SpecInputs`</param>
    /// <param name="tspProjectPath">Absolute path to the local TypeSpec project directory. Optional for custom-code-only scope.</param>
    /// <param name="editScope">Which source categories the tool may edit (custom code, spec inputs, or both).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="maxAttempts">Maximum custom-code repair attempts in one conversation (1..10).</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> indicating the outcome.</returns>
    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Applies customizations and validates regeneration/build. CustomCode supports maxAttempts (1..10, default 1) in one retained conversation; returns attemptsUsed and final failure diagnostics in the existing response.")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(
        [Description("Description of the requested customization to apply to the TypeSpec or SDK code. Can also be an APIView URL for feedback-driven customizations. REQUIRED.")]
        string customizationRequest,
        [Description("Absolute path to the SDK package directory. Required for `CustomCode` and `All`; not required for `SpecInputs`. Example: 'path/to/azure-sdk-for-java/sdk/healthdataaiservices/azure-health-deidentification'.")]
        string packagePath = null,
        [Description("Absolute path to the local TypeSpec project directory (containing main.tsp/client.tsp) where customizations will be applied. REQUIRED when editScope includes spec inputs (SpecInputs/All). OPTIONAL for custom-code-only repair (editScope CustomCode): when omitted, regeneration resolves the spec from the pinned commit in the package's tsp-location.yaml, so no local spec checkout is required. Example: 'path/to/azure-rest-api-specs/specification/healthdataaiservices/HealthDataAIServices.DeidServices'.")]
        string? tspProjectPath = null,
        [Description("Which source categories the tool may edit (flags: CustomCode, SpecInputs, or All). All (default): both custom code and spec inputs may be edited, regenerate, and patch custom code. CustomCode: custom-code-only — never edits spec inputs (client.tsp/tspconfig.yaml) or moves the pinned spec commit; failures that would require a spec change are reported as out of scope (errorCode 'SpecChangeRequired') instead of applied. Regenerating Generated/ from the unchanged pinned commit is always allowed.")]
        EditScope editScope = EditScope.All,
        CancellationToken ct = default,
        [Description("Maximum custom-code repair attempts in one retained conversation (1..10, default 1). Values above 1 require CustomCode scope.")]
        int maxAttempts = 1)
        => RunUpdateAsync(packagePath, tspProjectPath, customizationRequest, editScope, ct, maxAttempts);

    /// <summary>
    /// Executes the update pipeline: classify → patch customizations → regen → build.
    /// </summary>
    /// <param name="packagePath">Absolute path to the SDK package directory.</param>
    /// <param name="tspProjectPath">Absolute path to the local TypeSpec project directory.</param>
    /// <param name="customizationRequest">Description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="editScope">Which source categories the tool may edit (custom code, spec inputs, or both).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> with the pipeline result.</returns>
    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string? packagePath, string? tspProjectPath, string customizationRequest, EditScope editScope, CancellationToken ct, int maxAttempts)
    {
        if (maxAttempts is < 1 or > 10 || (maxAttempts > 1 && editScope != EditScope.CustomCode))
        {
            return CreateInvalidInputResponse("maxAttempts must be between 1 and 10; multiple attempts require CustomCode scope.");
        }
        // editScope is a non-nullable [Flags] enum bound from a named option (default All), so the
        // empty/whitespace validation used for the string inputs does not apply. Guard only against an
        // undefined value (a stray flag bit outside the All mask, or an empty 0 combination) so every
        // downstream HasFlag check operates on a valid CustomCode/SpecInputs combination.
        if (editScope == 0 || (editScope & ~EditScope.All) != 0)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Invalid editScope value: {(int)editScope}. Must be a combination of CustomCode, SpecInputs, or All.",
                Message = $"Invalid editScope value: {(int)editScope}. Must be a combination of CustomCode, SpecInputs, or All.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = $"Invalid editScope value: {(int)editScope}."
            };
        }

        var specInputsInScope = editScope.HasFlag(EditScope.SpecInputs);
        var customCodeInScope = editScope.HasFlag(EditScope.CustomCode);
        string? repoRoot = null;
        
        var validSdkRepoPackagePath = true;

        LanguageService? languageService = null;

        PackageInfo? packageInfo = null;

        var hasPackagePath = !string.IsNullOrWhiteSpace(packagePath);
        if (customCodeInScope && !hasPackagePath)
        {
            const string message = "Package path is required when editScope includes CustomCode (editScope includes CustomCode/All), because the tool must edit customization code in the package. Provide --package-path.";
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = message,
                Message = message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = message
            };
        }

        if (hasPackagePath)
        {
            if (!Directory.Exists(packagePath))
            {
                logger.LogError("Package path does not exist: {PackagePath}", packagePath);
                validSdkRepoPackagePath = false;
                if (customCodeInScope)
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
            }
            else
            {
                // Discover the Git repository root for the package path, and validate that the package path is within a Git repository, if not, it is not a valid package path.
                try
                {
                    repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
                    if (string.IsNullOrWhiteSpace(repoRoot))
                    {
                        logger.LogError("Package path is not within a Git repository: {PackagePath}", packagePath);
                        validSdkRepoPackagePath = false;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to discover Git repository root for package path: {PackagePath}", packagePath);
                    validSdkRepoPackagePath = false;
                }
            }
        }

        // Validate input
        if (customCodeInScope && !validSdkRepoPackagePath)
        {
            const string packagePathMessage = "A valid package path which is a local cloned SDK repo is required when custom code is in scope " +
                "(editScope includes CustomCode/All), because the tool must edit SDK source code locally. " +
                "Provide --package-path, or use editScope specInputs to repair typespec only.";
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = packagePathMessage,
                Message = packagePathMessage,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = packagePathMessage
            };
        }

        // tspProjectPath is only required when spec inputs are in scope (the tool must edit a local
        // client.tsp/tspconfig.yaml). For custom-code-only repair it is optional: regeneration resolves
        // the spec from the pinned commit recorded in the package's tsp-location.yaml, so a local spec
        // checkout is not required (this is what enables headless custom-code repair in a language repo).
        var hasTspProjectPath = !string.IsNullOrWhiteSpace(tspProjectPath);

        if (specInputsInScope && !hasTspProjectPath)
        {
            const string message = "A TypeSpec project path is required when spec inputs are in scope " +
                "(editScope includes SpecInputs/All), because the tool must edit client.tsp/tspconfig.yaml locally. " +
                "Provide --tsp-project-path, or use editScope CustomCode to repair custom (non-generated) code only.";
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = message,
                Message = message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = message
            };
        }

        // When a path is supplied it must be a valid local TypeSpec project, regardless of scope.
        if (hasTspProjectPath)
        {
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
        }

        // Detect if customizationRequest is an APIView URL (prod or staging)
        string? apiViewUrl = IsApiViewUrl(customizationRequest) ? customizationRequest : null;

        try
        {
            languageService = await ResolveLanguageServiceAsync(packagePath, apiViewUrl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve language service for {PackagePath}", packagePath);
        }

        if (languageService != null)
        {
            logger.LogInformation("Resolved package info for package path {PackagePath}: {Language}", packagePath, languageService.Language);
            try
            {
                packageInfo = await languageService.GetPackageInfo(packagePath, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve package info for {PackagePath}", packagePath);
            }
        }

        // When spec inputs are out of scope: items that can only be fixed by a spec change
        // (client.tsp/tspconfig.yaml). Declared before CreateResponse so every response can surface them.
        List<string> specChangeRequired = new();

        // When custom code is out of scope: items that can only be fixed by editing customization code.
        // Reported, not applied, when EditScope.CustomCode is not set.
        List<string> customCodeChangeRequired = new();

        List<string> changesMade = new();
        var attemptsUsed = 0;
        List<AppliedPatch> appliedPatches = [];
        string? lastRepairError = null;

        CustomizedCodeUpdateResponse CreateResponse(CustomizedCodeUpdateResponse response)
        {
            response.AttemptsUsed = attemptsUsed;
            if (appliedPatches.Count > 0) { response.AppliedPatches ??= appliedPatches; }
            if (!response.Success && string.IsNullOrWhiteSpace(response.ResponseError))
            {
                response.ResponseError = response.Message ?? "Customized code update did not complete.";
                if (!string.IsNullOrWhiteSpace(response.BuildResult))
                {
                    response.ResponseError += Environment.NewLine + response.BuildResult;
                }
            }
            response.PackageName ??= packageInfo?.PackageName; 
            response.Language = packageInfo?.Language ?? languageService?.Language ?? SdkLanguage.Unknown;
            response.PackageType = packageInfo?.SdkType ?? SdkType.Unknown;
            response.TypeSpecProject ??= packageInfo?.SpecProjectPath ?? tspProjectPath;
            if (specChangeRequired.Count > 0)
            {
                response.SpecChangeRequired ??= specChangeRequired;
            }
            if (customCodeChangeRequired.Count > 0)
            {
                response.CustomCodeChangeRequired ??= customCodeChangeRequired;
            }
            if (changesMade.Count > 0)
            {
                response.TypeSpecChangesSummary ??= changesMade;
            }
            return response;
        }

        try
        {
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
                language: languageService != null ? languageService.Language.ToString() : null,
                editScope: editScope,
                ct: ct);
        }
        catch (CopilotCliUnavailableException ex)
        {
            logger.LogError(ex, "GitHub Copilot CLI is not available.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = ex.Message,
                Message = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
                BuildResult = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Invalid input for feedback classification.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = ex.Message,
                Message = ex.Message,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = ex.Message
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Feedback classification failed unexpectedly.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Feedback classification failed: {ex.Message}",
                Message = $"Feedback classification failed: {ex.Message}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
                BuildResult = $"Feedback classification failed: {ex.Message}"
            });
        }
        var feedbackDictionary = feedbackItems.ToDictionary(i => i.Id, i => i);

        List<string> manualInterventions = new();
        StringBuilder codeCustomizationLog = new();
        StringBuilder tspFixFailedReasons = new();
        bool buildSucceeded = false;
        string? buildError = null;
        string? requiredRegenerationError = null;

        if (response.Classifications == null || response.Classifications.Count == 0)
        {
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = "Feedback could not be classified.",
                Message = "Feedback could not be classified.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                BuildResult = "Feedback could not be classified."
            });
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

                // When spec inputs are out of scope: a TSP_APPLICABLE item can only be fixed by editing
                // spec inputs (client.tsp/tspconfig.yaml), which belongs in a separate spec-repo PR.
                // Never apply it here — record it as out of scope and continue with custom-code fixes.
                if (!specInputsInScope)
                {
                    logger.LogInformation("Spec inputs out of scope: item '{ItemId}' requires a spec change; reporting instead of applying.", itemDetails.ItemId);
                    specChangeRequired.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                    feedbackDictionary.Remove(itemDetails.ItemId);
                    continue;
                }

                logger.LogDebug("Applying tsp customization for: {feedback}", itemDetails.Text);
                var languageTaggedRequest = languageService != null ? $"For {languageService.Language}: {itemDetails.Text}" : itemDetails.Text;
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

                //custom code out of scope: remove TSP_APPLICABLE items from feedback dictionary so they are not re-classified in the second pass
                if (!customCodeInScope)
                {
                    feedbackDictionary.Remove(itemDetails.ItemId);
                }
            }
            else if (itemDetails.Classification == ClassificationCodeCustomization)
            {
                // When custom code is out of scope: a CODE_CUSTOMIZATION item can only be fixed by editing
                // customization code, which the current edit scope does not permit. Record and skip patching.
                if (!customCodeInScope)
                {
                    logger.LogInformation("Custom code out of scope: item '{ItemId}' requires a custom-code change; reporting instead of patching.", itemDetails.ItemId);
                    customCodeChangeRequired.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                    feedbackDictionary.Remove(itemDetails.ItemId);
                    continue;
                }

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

        // Spec inputs out of scope: there is no custom code to patch and one or more items require a
        // spec change, so there is nothing this scope can apply. Stop and report — a human routes the
        // spec-change items to a separate spec-repo PR (manual-intervention items, if any, are surfaced
        // via NextSteps). This scope never edits spec inputs.
        if (!specInputsInScope && specChangeRequired.Count > 0 && codeCustomizations == 0)
        {
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "Out of scope: one or more items require a spec change (client.tsp/tspconfig.yaml), which belongs in a separate spec-repo PR, and there is no custom code to patch.",
                SpecChangeRequired = specChangeRequired,
                NextSteps = manualInterventions.Count > 0 ? manualInterventions : null,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.SpecChangeRequired
            });
        }

        // Nothing was classified as tsp applicable and at least some feedback requires manual intervention
        if (tspApplicable == 0 && codeCustomizations == 0 && customCodeChangeRequired.Count == 0 && manualChanges > 0)
        {
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "The requested changes require manual intervention and cannot be applied via TypeSpec customizations.",
                NextSteps = manualInterventions,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired
            });
        }
        // Everything was classified as success
        if (tspApplicable == 0 && codeCustomizations == 0 && customCodeChangeRequired.Count == 0 && noChanges > 0)
        {
            if (!customCodeInScope)
            {
                return CreateResponse(new CustomizedCodeUpdateResponse
                {
                    Success = true,
                    Message = "No changes needed — the requested customizations are already in place."
                });
            }
            if (languageService == null)
            {
                return CreateResponse(new CustomizedCodeUpdateResponse
                {
                    Message = "No language service is available to validate the SDK build.",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService
                });
            }
            var initialBuild = await languageService.BuildAsync(packagePath, null, CommandTimeoutInMinutes, ct);
            ct.ThrowIfCancellationRequested();
            buildSucceeded = initialBuild.Success;
            buildError = initialBuild.ErrorMessage ?? (buildSucceeded ? null : "Build failed without diagnostics.");
            if (buildSucceeded)
            {
                return CreateResponse(new CustomizedCodeUpdateResponse
                {
                    Success = true,
                    Message = "No changes needed — the SDK build passed."
                });
            }
        }
        
        //CustomCode out of scope, there is no futher code customization to apply, exit the tool with response
        if (!customCodeInScope)
        {
            var message = "";
            if (tspFixFailed > 0 || customCodeChangeRequired.Count > 0)
            {
                //CustomCode out of scope and some items require a custom-code change (CODE_CUSTOMIZATION) or TSP_APPLICABLE items failed to apply. Report as out of scope.
                message = "Out of scope:";
                if (tspFixFailed > 0)
                {
                    message += $" Some TSP_APPLICABLE items failed to apply and cannot be fixed in the current scope.";
                }
                if (customCodeChangeRequired.Count > 0)
                {
                    message += $" One or more items require a custom-code change, which is not allowed in the current edit scope.";
                }
                return CreateResponse(new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    Message = message,
                    CustomCodeChangeRequired = customCodeChangeRequired,
                    NextSteps = manualInterventions.Count > 0 ? manualInterventions : null,
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.CustomCodeChangeRequired
                });
            }
            else
            {
                //CustomCode out of scope and there is no more feedback in SpecInput scope to process, return success
                return CreateResponse(new CustomizedCodeUpdateResponse
                {
                    Success = true,
                    Message = "No additional changes are required for the specInput-only scope; however, custom code modifications may still be necessary.",
                    TypeSpecChangesSummary = changesMade.Count > 0 ? changesMade : null,
                    NextSteps = manualInterventions.Count > 0 ? manualInterventions : null,
                });
            }
        }

        // If custom code is in scope, a language service must be available for the package path to apply custom code changes.
        if (languageService == null)
        {
            return new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"No language service available for package path: {packagePath}",
                Message = $"No language service available for package path: {packagePath}. CustomCode is in scope, language service must be available for the package path to apply custom code changes.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService,
                BuildResult = $"No language service available for package path: {packagePath}"
            };
        }
        // ── Regen + Build if TSP fixes were applied and custom code is in scope and packagePath is valid sdk repo path ──
        if (tspFixSucceeded > 0 && customCodeInScope && validSdkRepoPackagePath)
        {
            logger.LogDebug("Regenerating {packagePath}", packagePath);

            if (!TryResolveLocalSpecProjectPath(tspProjectPath, MissingTypeSpecProjectPathMessage, out var localSpecProjectPath, out var errorResponse))
            {
                return errorResponse!;
            }

            logger.LogDebug("Using local spec project for regeneration: {localSpecProjectPath}", localSpecProjectPath);
            if (repoRoot != null)
            {
                await languageService.PreGenerateAsync(repoRoot, ct);
            }
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, localSpecRepoPath: localSpecProjectPath, isCli: false, ct: ct);
            if (!regenResult.IsSuccessful)
            {
                requiredRegenerationError = regenResult.ResponseError ?? "Regeneration failed without diagnostics.";
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
                var (success, error, _) = await languageService.BuildAsync(packagePath, additionalArguments: null, timeoutMinutes: CommandTimeoutInMinutes, ct: ct);
                buildSucceeded = success;
                buildError = error;

                if (buildSucceeded && codeCustomizations == 0)
                {
                    logger.LogInformation("Build passed after TypeSpec customizations.");
                    return CreateResponse(new CustomizedCodeUpdateResponse
                    {
                        Success = manualInterventions.Count == 0,
                        Message = manualInterventions.Count == 0
                            ? "Build passed after attempting TypeSpec customizations."
                            : "Build passed after attempting TypeSpec customizations, but some items require manual intervention.",
                        TypeSpecChangesSummary = changesMade,
                        NextSteps = manualInterventions,
                        ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
                    });
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

            var secondResponse = await _classifierService.ClassifyItemsAsync([.. feedbackDictionary.Values], globalContext: string.Join(";", changesMade), tspProjectPath: tspProjectPath, language: languageService.Language.ToString(), editScope: editScope, ct: ct);

            if (secondResponse.Classifications != null)
            {
                foreach (var itemDetails in secondResponse.Classifications)
                {
                    if (itemDetails.Classification == ClassificationCodeCustomization)
                    {
                        if (!customCodeInScope)
                        {
                            logger.LogInformation("Custom code out of scope: item '{ItemId}' reclassified as CODE_CUSTOMIZATION on second pass; reporting instead of patching.", itemDetails.ItemId);
                            customCodeChangeRequired.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                            feedbackDictionary.Remove(itemDetails.ItemId);
                            continue;
                        }

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
                    else if (itemDetails.Classification == ClassificationTspApplicable)
                    {
                        // Spec inputs out of scope — surface as out of scope instead of applying.
                        logger.LogInformation("Spec inputs out of scope: item '{ItemId}' reclassified as TSP_APPLICABLE on second pass; reporting.", itemDetails.ItemId);
                        specChangeRequired.Add($"'{itemDetails.Text}' (Reason: {itemDetails.Reason})");
                        feedbackDictionary.Remove(itemDetails.ItemId);
                    }
                }
            }
        }

        // Data tracking (per Laurent/Sam): record the spec-vs-custom-code split so we can measure how often
        // the tool falls back to custom-code workarounds vs. items that belong in client.tsp. A high
        // custom-code ratio is a signal that "shift-left" is not catching enough upstream. This is emitted
        // for every run regardless of edit scope so the split is observable in logs/telemetry.
        logger.LogInformation(
            "Customized code update split (editScope={EditScope}): tspApplied={TspApplied}, codeCustomizations={CodeCustomizations}, specChangeRequired={SpecChangeRequired}, customCodeChangeRequired={CustomCodeChangeRequired}, manualIntervention={ManualIntervention}",
            editScope, tspFixSucceeded, codeCustomizations, specChangeRequired.Count, customCodeChangeRequired.Count, manualInterventions.Count);

        // Build for error context if no build happened yet (pure CODE_CUSTOMIZATION path or regen failed)
        if (!buildSucceeded && buildError == null)
        {
            logger.LogInformation("Building for error context...");
            var (s, e, _) = await languageService.BuildAsync(packagePath, additionalArguments: null, timeoutMinutes: CommandTimeoutInMinutes, ct: ct);
            buildSucceeded = s;
            buildError = e;
        }

        if (requiredRegenerationError != null && codeCustomizations == 0)
        {
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Message = "Required regeneration failed after TypeSpec changes.",
                BuildResult = requiredRegenerationError,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateFailed,
                NextSteps = manualInterventions
            });
        }

        if (buildSucceeded && codeCustomizations == 0)
        {
            logger.LogInformation("Build passed after TypeSpec customizations.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = manualInterventions.Count == 0,
                Message = manualInterventions.Count == 0
                    ? "Build passed after attempting TypeSpec customizations."
                    : "Build passed after attempting TypeSpec customizations, but some items require manual intervention.",
                TypeSpecChangesSummary = changesMade,
                NextSteps = manualInterventions,
                ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
            });
        }

        // Step 2: If the build failed or CODE_CUSTOMIZATION items still need patching, start customized code update process

        // Custom code out of scope: never patch custom code in this edit scope. The build still failed
        // (or items remain), but those can only be fixed by editing customization code, which the current
        // scope does not permit. Stop and report so a human can address the custom-code changes separately.
        if (!customCodeInScope)
        {
            logger.LogInformation("Custom code out of scope: skipping customized code patching.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                Message = "Out of scope: remaining build failures require custom-code changes, which are not permitted by the current edit scope.",
                TypeSpecChangesSummary = changesMade.Count > 0 ? changesMade : null,
                CustomCodeChangeRequired = customCodeChangeRequired.Count > 0 ? customCodeChangeRequired : null,
                NextSteps = manualInterventions.Count > 0 ? manualInterventions : null,
                BuildResult = buildError,
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.CustomCodeChangeRequired
            });
        }

        if (!languageService.IsCustomizedCodeUpdateSupported)
        {
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = "Language service does not support customized code updates.",
                Message = "Language service does not support customized code updates.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.NoLanguageService,
                BuildResult = "No language service available for this package type."
            });
        }

        // Step 3: Check for customization files to repair
        var customizationRoot = languageService.HasCustomizations(packagePath, ct);
        if (customizationRoot == null)
        {
            logger.LogInformation("Build failed but no customization files found.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = string.IsNullOrWhiteSpace(buildError)
                    ? "Build failed but no customization files found to repair."
                    : $"Build failed but no customization files found to repair.\n{buildError}",
                Message = "Build failed but no customization files found to repair.",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildNoCustomizationsFailed,
                BuildResult = buildError
            });
        }

        // Step 4: Apply patches based on build errors
        var patchContext = BuildPatchContext(customizationRequest, codeCustomizationLog, buildError);
        var lastValidatedPatchCount = 0;
        var finalBuildSuccess = false;
        var failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed;
        var failureMessage = "Code customization patches applied but build still failing.";
        string? stopReason = null;
        lastRepairError = buildError;

        async Task<CopilotAgentValidationResult> ValidatePatchesAsync(IReadOnlyList<AppliedPatch> patches)
        {
            ct.ThrowIfCancellationRequested();
            if (attemptsUsed >= maxAttempts)
            {
                throw new InvalidOperationException("The repair attempt limit was reached.");
            }
            attemptsUsed++;
            finalBuildSuccess = false;
            appliedPatches = [.. patches];
            if (patches.Count <= lastValidatedPatchCount)
            {
                failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed;
                failureMessage = "No patches could be applied - automated repair made no further progress.";
                stopReason = "No additional patches were applied.";
                throw new InvalidOperationException(stopReason);
            }
            lastValidatedPatchCount = patches.Count;
            var phase = "Preparation";
            try
            {
                // Keep preparation's existing fallback policy and the unchanged pinned/local spec inputs.
                if (languageService.Language is SdkLanguage.Java or SdkLanguage.DotNet)
                {
                    failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.RegenerateAfterPatchesFailed;
                    if (repoRoot != null) { await languageService.PreGenerateAsync(repoRoot, ct); }
                    ct.ThrowIfCancellationRequested();
                    phase = "Regeneration";
                    var localSpec = string.IsNullOrWhiteSpace(tspProjectPath) ? null : Path.GetFullPath(tspProjectPath);
                    var generated = await tspClientHelper.UpdateGenerationAsync(packagePath, localSpecRepoPath: localSpec, isCli: false, ct: ct);
                    ct.ThrowIfCancellationRequested();
                    if (!generated.IsSuccessful)
                    {
                        lastRepairError = generated.ResponseError ?? "Regeneration failed without diagnostics.";
                        failureMessage = "Regeneration failed after patches.";
                        return FailedValidation();
                    }
                }
                phase = "Build";
                failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.BuildAfterPatchesFailed;
                failureMessage = "Code customization patches applied but build still failing.";
                var build = await languageService.BuildAsync(packagePath, null, CommandTimeoutInMinutes, ct);
                ct.ThrowIfCancellationRequested();
                finalBuildSuccess = build.Success;
                lastRepairError = build.ErrorMessage ?? (build.Success ? null : "Build failed without diagnostics.");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (stopReason == null)
            {
                logger.LogError(ex, "{Phase} failed during repair attempt {Attempt}", phase, attemptsUsed);
                lastRepairError = ex.Message;
                failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError;
                failureMessage = $"{phase} failed after patches.";
            }
            return finalBuildSuccess ? new() { Success = true } : FailedValidation();
        }

        CopilotAgentValidationResult FailedValidation()
        {
            if (attemptsUsed >= maxAttempts)
            {
                stopReason = $"Repair attempt limit ({maxAttempts}) reached.";
                throw new InvalidOperationException($"{stopReason}\n{lastRepairError}");
            }
            return new()
            {
                Success = false,
                Reason = $"Attempt {attemptsUsed}/{maxAttempts}: {failureMessage}\n{lastRepairError}\n" +
                    "Continue from the existing edits and conversation; do not repeat an unsuccessful patch."
            };
        }

        var patches = await languageService.ApplyPatchesAsync(
            customizationRoot, packagePath, patchContext, ct, maxAttempts, ValidatePatchesAsync);
        ct.ThrowIfCancellationRequested();
        appliedPatches = patches;
        if (attemptsUsed == maxAttempts && patches.Count != lastValidatedPatchCount)
        {
            finalBuildSuccess = false;
            stopReason = "Additional patches were not validated within the repair attempt limit.";
        }
        // A session may terminate without Exit after applying tools. Validate that final batch too.
        if (patches.Count > lastValidatedPatchCount && attemptsUsed < maxAttempts)
        {
            try { await ValidatePatchesAsync(patches); }
            catch (InvalidOperationException) when (stopReason != null) { }
        }
        if (attemptsUsed == 0 && patches.Count == 0)
        {
            failureCode = CustomizedCodeUpdateResponse.KnownErrorCodes.PatchesFailed;
            failureMessage = "No patches could be applied - the agent returned no patch proposal for validation.";
        }

        if (finalBuildSuccess)
        {
            logger.LogInformation("Build passed after code customization patches.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = manualInterventions.Count == 0,
                Message = manualInterventions.Count == 0
                    ? "Build passed after code customization patches."
                    : "Build passed after code customization patches, but some items require manual intervention.",
                TypeSpecChangesSummary = changesMade,
                AppliedPatches = appliedPatches,
                SpecChangeRequired = specChangeRequired.Count > 0 ? specChangeRequired : null,
                NextSteps = manualInterventions,
                ErrorCode = manualInterventions.Count > 0 ? CustomizedCodeUpdateResponse.KnownErrorCodes.ManualInterventionRequired : null,
            });
        }

        // Build still failing after patches
        logger.LogInformation("Customized code repair did not complete after {AttemptsUsed} attempts.", attemptsUsed);
        return CreateResponse(new CustomizedCodeUpdateResponse
        {
            Success = false,
            ResponseError = $"{failureMessage} {stopReason ?? "The patch agent stopped before validation succeeded."} " +
                $"Attempts used: {attemptsUsed}.\n{lastRepairError}",
            Message = failureMessage,
            ErrorCode = failureCode,
            BuildResult = lastRepairError,
            TypeSpecChangesSummary = changesMade,
            AppliedPatches = patches,
            SpecChangeRequired = specChangeRequired.Count > 0 ? specChangeRequired : null,
            NextSteps = manualInterventions,
        });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during customized code update.");
            return CreateResponse(new CustomizedCodeUpdateResponse
            {
                Success = false,
                ResponseError = $"Unexpected error: {ex.Message}",
                Message = $"Unexpected error: {ex.Message}",
                ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.UnexpectedError,
                BuildResult = ex.Message
            });
        }
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

    private bool TryResolveLocalSpecProjectPath(
        string? tspProjectPath,
        string missingPathMessage,
        out string? localSpecProjectPath,
        out CustomizedCodeUpdateResponse? errorResponse)
    {
        localSpecProjectPath = null;
        errorResponse = null;

        if (string.IsNullOrWhiteSpace(tspProjectPath))
        {
            logger.LogError("{ErrorMessage}", missingPathMessage);
            errorResponse = CreateInvalidInputResponse(missingPathMessage);
            return false;
        }

        localSpecProjectPath = Path.GetFullPath(tspProjectPath);
        return true;
    }

    private static CustomizedCodeUpdateResponse CreateInvalidInputResponse(string message) => new()
    {
        Success = false,
        ResponseError = message,
        Message = message,
        ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
        BuildResult = message
    };

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
