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
        this.feedbackService = feedbackService;
        _classifierService = classifierService;
        this.typeSpecCustomizationService = typeSpecCustomizationService;
        this.typeSpecHelper = typeSpecHelper;
    }

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.TypeSpec, SharedCommandGroups.TypeSpecClient];

    private readonly Option<string> customizationRequestOption = new("--customization-request")
    {
        Description = "Description of the requested customization to apply to the TypeSpec.",
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<string> typespecProjectPath = new("--tsp-project-path")
    {
        Description = "Absolute path to the local TypeSpec project directory (containing main.tsp/client.tsp) where customizations will be applied.",
        Arity = ArgumentArity.ExactlyOne
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
    /// <param name="customizationRequest">Optional description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> indicating the outcome.</returns>
    [McpServerTool(Name = CustomizedCodeUpdateToolName), Description("Applies patches to customization files based on build errors, regenerates code if needed (Java), builds, and returns success/failure with build result.")]
    public Task<CustomizedCodeUpdateResponse> UpdateAsync(string packagePath, string tspProjectPath, string? customizationRequest = null, CancellationToken ct = default)
        => RunUpdateAsync(packagePath, tspProjectPath, customizationRequest, ct);

    /// <summary>
    /// Executes the update pipeline: classify → patch customizations → regen → build.
    /// </summary>
    /// <param name="packagePath">Absolute path to the SDK package directory.</param>
    /// <param name="tspProjectPath"></param>
    /// <param name="customizationRequest">Optional description of the requested customization to apply to the TypeSpec, used for guiding the update process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CustomizedCodeUpdateResponse"/> with the pipeline result.</returns>
    private async Task<CustomizedCodeUpdateResponse> RunUpdateAsync(string packagePath, string tspProjectPath, string? customizationRequest, CancellationToken ct)
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

        // Get language info
        var languageService = await GetLanguageServiceAsync(packagePath, ct);


        // Step 1: try tsp fixes
        var tries = 0;
        var feedbackToAddress = customizationRequest;
        var maxTries = 2;
        bool buildSucceeded = false;
        string? buildError = null;
        do
        {
            var response = await Classify(tspProjectPath, plainTextFeedback: feedbackToAddress, ct: ct);

            if (response.Classifications == null || response.Classifications.Count == 0) // TODO - do we want to fail if classification returns nothing?
            {
                if (tries == 0)
                {
                    return new CustomizedCodeUpdateResponse
                    {
                        Success = false,
                        Message = "Feedback could not be classified.", // TODO - tune error message
                        ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.InvalidInput,
                        BuildResult = "Feedback could not be classified."
                    };
                }

                // On subsequent iterations, no classifications means nothing more to fix via TSP
                logger.LogInformation("No further TSP-applicable classifications found on iteration {Iteration}.", tries + 1);
                break;
            }

            var failed = 0;
            var succeeded = 0;
            var attempted = 0;
            StringBuilder failureReasons = new();
            StringBuilder changesMade = new();

            foreach (var feedbackItem in response.Classifications)
            {
                if (feedbackItem.Classification == "TSP_APPLICABLE")
                {
                    attempted++;
                    var tspCustomizationResult = await typeSpecCustomizationService.ApplyCustomizationAsync(tspProjectPath, feedbackItem.Text, ct: ct);

                    if (tspCustomizationResult.Success)
                    {
                        var newChanges = string.Join("; ", tspCustomizationResult.ChangesSummary);
                        if (changesMade.Length > 0)
                        {
                            changesMade.AppendLine();
                        }
                        changesMade.Append(newChanges);
                        succeeded++;
                    }
                    else
                    {
                        failureReasons.Append(tspCustomizationResult.FailureReason);
                        failureReasons.Append("; ");
                        failed++;
                    }
                }
            }

            // None of the attempted changes succeeded
            if (succeeded == 0 && attempted > 0)
            {
                return new CustomizedCodeUpdateResponse
                {
                    Success = false,
                    Message = $"Failed to apply TypeSpec customizations: {failureReasons.ToString()}",
                    ErrorCode = CustomizedCodeUpdateResponse.KnownErrorCodes.TypeSpecCustomizationFailed,
                    BuildResult = $"Failed to apply TypeSpec customizations: {failureReasons.ToString()}",
                };
            }

            if (failed > 0)
            {
                logger.LogWarning("Some customizations failed to apply: {FailureReasons}", failureReasons.ToString());
            }

            var (success, error, _) = await languageService.BuildAsync(packagePath, CommandTimeoutInMinutes, ct);

            buildSucceeded = success;
            buildError = error;
            tries++;

            if (tries < maxTries) // Don't do this work if this is the last try
            {
                StringBuilder iterationContext = new();

                iterationContext.AppendLine(customizationRequest);
                iterationContext.AppendLine($"---- ITERATION {tries + 1}-------");
                if (changesMade.Length > 0)
                {
                    iterationContext.AppendLine("CHANGES MADE:");
                    iterationContext.Append(changesMade);
                    iterationContext.AppendLine();
                }
                if (failureReasons.Length > 0)
                {
                    iterationContext.AppendLine($"CUSTOMIZATIONS THAT FAILED TO APPLY ({failed} of {attempted}):");
                    iterationContext.AppendLine(failureReasons.ToString());
                }
                iterationContext.AppendLine($"BUILD OUTPUT: {error}");

                // for next iteration, feed build error back into classification to see if there are additional
                // fixes that can be applied based on new build errors after tsp fixes
                feedbackToAddress = iterationContext.ToString();
            }
        } while (buildSucceeded == false && tries < maxTries);

        if (buildSucceeded)
        {
            logger.LogInformation("Build passed - no repairs needed.");
            return new CustomizedCodeUpdateResponse
            {
                Success = true,
                Message = "Build passed - no repairs needed."
            };
        }

        // Step 2: Start customized code update process

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
        logger.LogInformation("Applying patches to fix build errors...");
        var patches = await languageService.ApplyPatchesAsync(
            customizationRoot,
            packagePath,
            buildError ?? string.Empty,
            ct);

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
            var regenResult = await tspClientHelper.UpdateGenerationAsync(packagePath, commitSha: null, isCli: false, localSpecRepoPath: null, ct);
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
        string? apiViewUrl = default,
        string? plainTextFeedback = default,
        string? plainTextFeedbackFile = default,
        string? language = default,
        string? serviceName = default,
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

            if (string.IsNullOrEmpty(tspProjectPath) || !Directory.Exists(tspProjectPath))
            {
                throw new DirectoryNotFoundException($"TypeSpec project path does not exist: {tspProjectPath}");
            }

            List<FeedbackItem> feedbackItems = [];
            if (!string.IsNullOrWhiteSpace(apiViewUrl))
            {
                feedbackItems = await feedbackService.GetFeedbackItemsAsync(apiViewUrl, ct);
                language ??= await feedbackService.GetLanguageAsync(apiViewUrl, ct);
            }
            else if (!string.IsNullOrWhiteSpace(plainTextFeedback))
            {
                feedbackItems = [new FeedbackItem { Text = plainTextFeedback, Context = string.Empty }];
            }

            return await _classifierService.ClassifyItemsAsync(feedbackItems, globalContext: "", tspProjectPath, language, serviceName, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Classification failed");
            throw;
        }
    }
}
