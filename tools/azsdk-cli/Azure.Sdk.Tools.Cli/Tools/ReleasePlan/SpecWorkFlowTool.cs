// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Web;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("This type contains the MCP tool to run SDK generation using pipeline, check SDK generation pipeline status and to get generated SDK pull request details.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IGitHubService githubService,
        IDevOpsService devopsService,
        ITypeSpecHelper typespecHelper,
        ILogger<SpecWorkflowTool> logger,
        IInputSanitizer inputSanitizer
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-workflow", "TypeSpec SDK generation commands")];

        // Commands
        private const string generateSdkCommandName = "generate-sdk";
        private const string getSdkPullRequestCommandName = "get-sdk-pr";
        private const string validateSdkRunCommandName = "validate-sdk-run";
        private const string completeSdkRunCommandName = "complete-sdk-run";

        // MCP Tool Names
        private const string RunGenerateSdkToolName = "azsdk_run_generate_sdk";
        private const string GetSdkPullRequestLinkToolName = "azsdk_get_sdk_pull_request_link";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "TypeSpec project path matching the stored target. Use the plan's repository-relative path to generate without a local clone.",
            Required = true,
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Optional spec pull request number; must match the PR linked to the release plan",
            Required = false,
        };

        private readonly Option<string> apiVersionOpt = new("--api-version")
        {
            Description = "Expected stored API version; defaults to the plan's version and cannot override it. Forwarded in both interactive and automated runs.",
            Required = false,
        };

        private readonly Option<bool> requireMergedSpecOpt = new("--require-merged-spec")
        {
            Description = "Opt in to sdk-release auto-release generation; the linked public PR must be merged at the stored SHA. Default is sdk-review: draft PRs without auto-release labels.",
        };

        private readonly Option<string> expectedSpecCommitShaOpt = new("--spec-commit-sha")
        {
            Description = "Expected stored SpecCommitSHA, not a new pin. Reject a changed target; generation never configures or backfills one.",
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--release-type")
        {
            Description = "SDK release type: beta or stable; must match the stored target. Forwarded in both interactive and automated runs.",
            Required = true,
        };

        private readonly Option<string> languageOpt = new("--language")
        {
            Description = "SDK language, Options[Python, .NET, JavaScript, Java, go]",
            Required = true,
        };

        private readonly Option<int> workItemIdOpt = new("--workitem-id")
        {
            Description = "SDK release plan work item id",
            Required = true,
        };

        private readonly Option<int> pipelineRunIdOpt = new("--pipeline-run")
        {
            Description = "SDK generation pipeline run id",
            Required = true,
        };

        private readonly Option<string> completedSdkPrOpt = new("--sdk-pr") { Description = "Generated SDK PR URL; may be empty for 'No changes' or 'Failed to generate SDK.'." };
        private readonly Option<string> completedStatusOpt = new("--status") { Description = "draft, ready for review, No changes, or Failed to generate SDK. Ready for review requires a saved sdk-release run.", Required = true };

        private static readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        public static readonly string ARM_SIGN_OFF_LABEL = "ARMSignedOff";

        public static readonly HashSet<string> SUPPORTED_LANGUAGES = new()
        {
            "python",
            ".net",
            "javascript",
            "java",
            "go"
        };

        protected override List<Command> GetCommands() =>
        [
            new McpCommand(generateSdkCommandName, "Generate SDK for a TypeSpec project", RunGenerateSdkToolName)
            {
                typeSpecProjectPathOpt, apiVersionOpt, sdkReleaseTypeOpt, languageOpt, pullRequestNumberOpt, workItemIdOpt, requireMergedSpecOpt, expectedSpecCommitShaOpt,
            },
            new McpCommand(getSdkPullRequestCommandName, "Get SDK pull request link from SDK generation pipeline", GetSdkPullRequestLinkToolName)
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt,
            },
            new Command(validateSdkRunCommandName, "Check a generation job's saved inputs and build ID against the current release target")
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt,
            },
            new Command(completeSdkRunCommandName, "Record generation results only if the job is still current")
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt, completedSdkPrOpt, completedStatusOpt,
            },
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;
            var commandParser = parseResult;
            return command switch
            {
                generateSdkCommandName => await RunGenerateSdkAsync(commandParser.GetValue(typeSpecProjectPathOpt),
                                        commandParser.GetValue(sdkReleaseTypeOpt),
                                        commandParser.GetValue(languageOpt),                                        
                                        commandParser.GetValue(pullRequestNumberOpt),
                                        commandParser.GetValue(workItemIdOpt),                                     
                                        commandParser.GetValue(apiVersionOpt),
                                        commandParser.GetValue(requireMergedSpecOpt),
                                        commandParser.GetValue(expectedSpecCommitShaOpt) ?? "",
                                        ct),
                getSdkPullRequestCommandName => await GetSDKPullRequestDetails(commandParser.GetValue(languageOpt), workItemId: commandParser.GetValue(workItemIdOpt), buildId: commandParser.GetValue(pipelineRunIdOpt), ct: ct),
                validateSdkRunCommandName => await ValidateOrCompleteSdkRunAsync(commandParser.GetValue(workItemIdOpt), commandParser.GetValue(pipelineRunIdOpt), commandParser.GetValue(languageOpt)!, ct: ct),
                completeSdkRunCommandName => await ValidateOrCompleteSdkRunAsync(commandParser.GetValue(workItemIdOpt), commandParser.GetValue(pipelineRunIdOpt), commandParser.GetValue(languageOpt)!,
                    commandParser.GetValue(completedSdkPrOpt) ?? "", commandParser.GetValue(completedStatusOpt), ct),
                _ => new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" },
            };
        }

        public async Task<ReleaseWorkflowResponse> ValidateOrCompleteSdkRunAsync(int workItemId, int buildId, string language, string sdkPrUrl = "", string? status = null, CancellationToken ct = default)
        {
            try
            {
                if (status != null)
                {
                    if (!await devopsService.CompleteSdkGenerationAsync(workItemId, buildId, language, sdkPrUrl, status, ct))
                    {
                        throw new InvalidOperationException("The SDK generation result was not recorded.");
                    }
                    return new ReleaseWorkflowResponse { Status = "Success", Details = [$"Recorded SDK generation result for build {buildId}. Its saved snapshot still matches release plan {workItemId}."] };
                }
                var build = await devopsService.ValidateSdkGenerationRunAsync(workItemId, buildId, language, ct);
                return new ReleaseWorkflowResponse
                {
                    Status = "Success",
                    Details = [$"Build {build.Id}: spec commit {build.SourceVersion}, API version {build.TemplateParameters["ApiVersion"]}, SDK release type {build.TemplateParameters["SdkReleaseType"]}. The job is still current for release plan {workItemId}. This validates job provenance, not generated-code correctness."]
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ReleaseWorkflowResponse { Status = "Failed", ResponseError = ex.Message };
            }
        }

        private async Task<ReleaseWorkflowResponse> IsSdkDetailsPresentInReleasePlanAsync(int workItemId, string language, CancellationToken ct)
        {
            var response = new ReleaseWorkflowResponse()
            {
                Status = "Failed",
                ResponseErrors = [],
            };

            try
            {
                if (workItemId == 0)
                {
                    response.ResponseErrors.Add("Work item ID is required to check if release plan is ready for SDK generation.");
                    return response;
                }

                var releasePlan = await devopsService.ResolveReleasePlanByIdAsync(workItemId, ct);

                var sdkInfoList = releasePlan?.SDKInfo;

                if (sdkInfoList == null || sdkInfoList.Count == 0)
                {
                    response.ResponseErrors.Add($"SDK details are not present in the release plan. Update the SDK details using the information in tspconfig.yaml");
                    return response;
                }

                var sdkInfo = sdkInfoList.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));

                if (sdkInfo == null || string.IsNullOrWhiteSpace(sdkInfo.Language))
                {
                    response.ResponseErrors.Add($"Release plan work item with ID {workItemId} does not have a language specified. Update the SDK details using the information in tspconfig.yaml.");
                    return response;
                }

                if (string.IsNullOrWhiteSpace(sdkInfo.PackageName))
                {
                    response.ResponseErrors.Add($"Release plan work item with ID {workItemId} does not have a package name specified for {sdkInfo.Language}. Update the SDK details using the information in tspconfig.yaml.");
                    return response;
                }
                response.SetLanguage(sdkInfo.Language);
                if (releasePlan?.IsManagementPlane == true)
                {
                    response.PackageType = SdkType.Management;
                }
                else if (releasePlan?.IsDataPlane == true)
                {
                    response.PackageType = SdkType.Dataplane;
                }
                response.Details.Add($"SDK info for language '{sdkInfo.Language}' and package '{sdkInfo.PackageName}' is set correctly in the release plan.");
                response.Status = "Success";
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.ResponseErrors.Add($"Failed to check if Release Plan is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }

        [McpServerTool(Name = RunGenerateSdkToolName), Description("Run pipeline SDK generation for a release plan, including no-local-clone and all-language requests (one call per language). " +
            "Read the plan first; pass its project path, SDK release type (beta or stable), language and plan/work item ID. Uses stored SpecAPIVersion and SpecCommitSHA without retargeting, backfilling or rerunning compiler validation; use the stored repository-relative path without a local clone. " +
            "Optional apiVersion and specCommitSha guard the stored target, not override it. Missing targets require preview and confirmation through release-plan update tools. API version and SDK release type are forwarded for interactive and automated runs. " +
            "Default sdk-review creates draft SDK PRs without auto-release labels. requireMergedSpec=true opts in to sdk-release and verifies the linked public PR's merge SHA matches the pin. Supported pre-merge review uses the confirmed PR HEAD SHA, never a synthetic merge commit. Private Preview is spec-only. " +
            "Do not use azsdk_release_sdk (package publishing) or azsdk_get_sdk_pull_request_link (link retrieval) to generate SDKs.")]
        public async Task<ReleaseWorkflowResponse> RunGenerateSdkAsync(string typespecProjectRoot, string sdkReleaseType, string language, int pullRequestNumber = 0, int workItemId = 0, string apiVersion = "", bool requireMergedSpec = false, string specCommitSha = "", CancellationToken ct = default)
        {
            try
            {
                var response = new ReleaseWorkflowResponse()
                {
                    Status = "Success",
                    ResponseErrors = []
                };

                if (workItemId == 0)
                {
                    response.ResponseErrors.Add("Release plan work item ID is required to run SDK generation.");
                    response.Status = "Failed";
                    response.NextSteps = ["Create a release plan if you don't have one or get existing release plan and re-run SDK generation."];
                    return response;
                }
                // The resolver accepts either a Release Plan ID or a work item ID.
                var releasePlan = await devopsService.ResolveReleasePlanByIdAsync(workItemId, ct);
                if (releasePlan == null)
                {
                    response.ResponseErrors.Add($"No release plan found for work item ID {workItemId}. Please check the work item ID and try again.");
                    response.Status = "Failed";
                    return response;
                }

                // The input may have been a Release Plan ID; use the resolved work item ID for subsequent calls.
                workItemId = releasePlan.WorkItemId;

                if (releasePlan.ApiReleaseType == ApiReleaseType.PrivatePreview)
                {
                    response.Details.Add($"Release plan with work item ID {workItemId} is in Private Preview stage. Important: SDK cannot be generated and released for private preview release plans and private preview release plan only requires to merge API spec PR. If required for validation purposes, you can generate the SDK locally only and only for SDK validation.");
                    response.Status = "Success";
                    return response;
                }

                language = inputSanitizer.SanitizeLanguage(language);
                var effectiveApiVersion = string.IsNullOrWhiteSpace(apiVersion) || apiVersion.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? releasePlan.SpecAPIVersion
                    : apiVersion;
                apiVersion = effectiveApiVersion;

                logger.LogInformation(
                    "Generating SDK for TypeSpec project: {TypespecProjectRoot}, API Version: {ApiVersion}, SDK Release Type: {SdkReleaseType}, Language: {Language}, Pull Request Number: {PullRequestNumber}, Work Item ID: {WorkItemId}",
                    typespecProjectRoot,
                    apiVersion,
                    sdkReleaseType,
                    language,
                    pullRequestNumber,
                    workItemId);
                // Is language supported for SDK generation
                if (!DevOpsService.IsSDKGenerationSupported(language))
                {
                    response.ResponseErrors.Add($"SDK generation is currently not supported by agent for {language}");
                    response.Status = "Failed";
                }
                response.SetLanguage(language);
                string typeSpecProjectPath = "";
                // Is valid typespec project path
                var requestedProject = typespecProjectRoot?.Replace('\\', '/').TrimEnd('/') ?? string.Empty;
                var storedProject = releasePlan.APISpecProjectPath.Replace('\\', '/').TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(storedProject) && string.Equals(requestedProject, storedProject, StringComparison.Ordinal))
                {
                    // The confirmed plan already identifies the remote project. Regeneration
                    // need not compile or check out the spec locally again.
                    typeSpecProjectPath = storedProject;
                    response.TypeSpecProject = storedProject;
                }
                else if (!TypeSpecProject.IsValidTypeSpecProjectPath(typespecProjectRoot))
                {
                    response.ResponseErrors.Add($"Invalid TypeSpec project root path [{typespecProjectRoot}].");
                    response.Status = "Failed";
                }
                else
                {
                    typeSpecProjectPath = (typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot) ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                    response.TypeSpecProject = typeSpecProjectPath;
                }

                List<string> validReleaseTypes = ["beta", "stable"];
                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                if (string.IsNullOrEmpty(sdkReleaseType) || !validReleaseTypes.Contains(sdkReleaseType))
                {
                    response.ResponseErrors.Add("SDK release type must be set as either beta or stable to generate SDK.");
                    response.Status = "Failed";
                }
                if (!string.IsNullOrWhiteSpace(releasePlan.SDKReleaseType) &&
                    !string.Equals(sdkReleaseType, releasePlan.SDKReleaseType, StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add("SDK release type does not match the release plan's confirmed target.");
                    response.Status = "Failed";
                }
                if (!string.IsNullOrEmpty(specCommitSha) && !string.Equals(specCommitSha, releasePlan.SpecCommitSHA, StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add("The release plan's spec commit changed. Retrieve and review the current target before generating.");
                    response.Status = "Failed";
                }

                if (sdkReleaseType.Equals("stable", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(effectiveApiVersion) &&
                    !effectiveApiVersion.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                    effectiveApiVersion.Contains("-preview", StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add($"Stable SDK generation is not allowed from preview API version '{effectiveApiVersion}'. Use SDK release type 'beta' or select a stable API version.");
                    response.Status = "Failed";
                    return response;
                }

                if (!string.IsNullOrWhiteSpace(releasePlan.SpecAPIVersion) &&
                    !string.Equals(apiVersion, releasePlan.SpecAPIVersion, StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add($"API version '{apiVersion}' does not match release plan API version '{releasePlan.SpecAPIVersion}'. Use the release plan for the intended API version instead of overriding this plan.");
                    response.Status = "Failed";
                    return response;
                }
                if (!string.IsNullOrWhiteSpace(releasePlan.APISpecProjectPath) &&
                    !string.Equals(typeSpecProjectPath, releasePlan.APISpecProjectPath.Replace('\\', '/').TrimEnd('/'), StringComparison.Ordinal))
                {
                    response.ResponseErrors.Add($"TypeSpec project '{typeSpecProjectPath}' does not match the release plan's project '{releasePlan.APISpecProjectPath}'.");
                    response.Status = "Failed";
                    return response;
                }

                // Update SDK details in release plan if work item ID is provided
                if (workItemId > 0)
                {
                    var readiness = await IsSdkDetailsPresentInReleasePlanAsync(workItemId, language, ct);
                    if (!readiness.Status.Equals("Success"))
                    {
                        response.ResponseErrors.AddRange(readiness.ResponseErrors);
                        response.Details.AddRange(readiness.Details);
                        response.Status = "Failed";
                    }
                    response.PackageType = readiness.PackageType;
                }

                // Return failure details in case of any failure
                if (response.Status.Equals("Failed"))
                {
                    var failureDetails = string.Join(",", response.ResponseErrors);
                    logger.LogInformation("SDK generation failed with details: [{FailureDetails}]", failureDetails);
                    return response;
                }

                // Do not regenerate an SDK that has already been released for this language.
                var sdkInfo = releasePlan.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));
                if (string.Equals(sdkInfo?.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "SDK for {Language} has already been released for release plan work item {WorkItemId}. Skipping SDK generation.",
                        language,
                        workItemId);
                    response.Status = "Success";
                    response.Details.Add($"SDK for {language} has already been released for release plan work item {workItemId}. A new SDK generation run was not triggered.");
                    return response;
                }

                // A pending generation request may not have a pipeline URL yet; do not queue another one.
                if (string.Equals(sdkInfo?.GenerationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("SDK generation for {Language} is Pending for release plan work item {WorkItemId}. Skipping a duplicate request.", language, workItemId);
                    response.Details.Add($"SDK generation for {language} is Pending for release plan work item {workItemId}. A new SDK generation run was not triggered to avoid duplicate generation.");
                    return response;
                }

                // In-progress labels can be stale. Only block when the saved pipeline is still active.
                // Old pipeline runs may have been archived and must not prevent regeneration.
                if (string.Equals(sdkInfo?.GenerationStatus, "In progress", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(sdkInfo?.GenerationPipelineUrl))
                {
                    var pipelineUrl = sdkInfo.GenerationPipelineUrl;
                    if (TryGetGenerationPipelineBuildId(pipelineUrl, out var buildId))
                    {
                        Build? previousRun = null;
                        try
                        {
                            previousRun = await devopsService.GetPipelineRunAsync(buildId, ct).WaitAsync(ct);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                        {
                            logger.LogWarning(ex, "Could not read SDK generation pipeline {PipelineUrl}; it may have been archived. Allowing generation for {Language}.", pipelineUrl, language);
                        }

                        var isRunning = previousRun?.Status is BuildStatus.NotStarted or BuildStatus.InProgress or BuildStatus.Postponed or BuildStatus.Cancelling;
                        if (previousRun != null && previousRun.Id == buildId && isRunning)
                        {
                            logger.LogInformation("SDK generation pipeline {PipelineUrl} is {Status}. Skipping a duplicate run for {Language}.", pipelineUrl, previousRun.Status, language);
                            response.Details.Add($"SDK generation for {language} already has a pipeline in status '{previousRun.Status}' for release plan work item {workItemId}. A new SDK generation run was not triggered to avoid duplicate generation. Previous SDK generation pipeline: {pipelineUrl}.");
                            return response;
                        }

                        logger.LogInformation("No active SDK generation run was confirmed at {PipelineUrl}. Allowing generation for {Language}.", pipelineUrl, language);
                    }
                    else
                    {
                        logger.LogWarning("The recorded SDK generation pipeline URL {PipelineUrl} is invalid. Allowing generation for {Language}.", pipelineUrl, language);
                    }
                }

                // Check if another active (in progress) release plan exists for the same TypeSpec project that already
                // has an SDK pull request that has not been released yet. If so, block SDK generation for the current
                // release plan to avoid conflicting/duplicate SDK pull requests for the same service.
                // Skip this check when the current release plan already has an SDK pull request for the same language,
                // so that regenerating the SDK for the current release plan is allowed.
                var currentSdkPullRequestUrl = sdkInfo?.SdkPullRequestUrl;
                if (string.IsNullOrEmpty(currentSdkPullRequestUrl))
                {
                    var activeReleasePlans = await devopsService.GetActiveReleasePlansByTypeSpecProjectPathAsync(typeSpecProjectPath, ct: ct);
                    var conflictingReleasePlan = activeReleasePlans?.FirstOrDefault(rp =>
                        rp.WorkItemId != workItemId &&
                        rp.SDKInfo.Any(s => s.Language == language &&
                            !string.IsNullOrEmpty(s.SdkPullRequestUrl) &&
                            !string.Equals(s.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase)));
                    if (conflictingReleasePlan != null)
                    {
                        var existingSdkPullRequests = conflictingReleasePlan.SDKInfo
                            .Where(s => !string.IsNullOrEmpty(s.SdkPullRequestUrl) &&
                                !string.Equals(s.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                            .Select(s => $"{s.Language}: {s.SdkPullRequestUrl}");
                        logger.LogInformation(
                            "Another active release plan (work item {ConflictingWorkItemId}) with SDK pull request(s) already exists for TypeSpec project {TypeSpecProjectPath}. Blocking SDK generation for release plan {WorkItemId}.",
                            conflictingReleasePlan.WorkItemId,
                            typeSpecProjectPath,
                            workItemId);
                        response.Status = "Failed";
                        var blockMessage = $"Another active release plan (work item {conflictingReleasePlan.WorkItemId}) with an SDK pull request already exists for this service. " +
                            "SDK can be generated for the current release plan only after completing the previous release plan or after abandoning it.";
                        response.ResponseErrors.Add(blockMessage);
                        response.Details.Add($"Existing release plan: {conflictingReleasePlan.ReleasePlanLink}");
                        response.Details.AddRange(existingSdkPullRequests.Select(pr => $"Existing SDK pull request: {pr}"));
                        response.NextSteps = ["Complete or abandon the previous release plan before generating the SDK for the current release plan."];
                        return response;
                    }
                }

                var (specRepository, linkedPullRequestNumber) = ReleasePlanSpecHelper.ParsePullRequest(releasePlan.ActiveSpecPullRequest);
                if (!string.Equals(specRepository, PUBLIC_SPECS_REPO, StringComparison.OrdinalIgnoreCase))
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add("SDK generation requires a linked spec PR in the public Azure/azure-rest-api-specs repository.");
                    return response;
                }
                if (pullRequestNumber > 0 && pullRequestNumber != linkedPullRequestNumber)
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add($"Spec PR {pullRequestNumber} does not match the release plan's linked PR {linkedPullRequestNumber}. Update the release plan's spec PR before regenerating with a different spec.");
                    return response;
                }

                specCommitSha = releasePlan.SpecCommitSHA;
                if (!ReleasePlanSpecHelper.IsValidCommitSha(specCommitSha) || string.IsNullOrWhiteSpace(releasePlan.SpecAPIVersion))
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add("The release plan has a missing or invalid spec commit SHA or API version. Preview and confirm the release target with update-spec-pr before generating SDKs; generation never chooses a new target implicitly.");
                    return response;
                }
                if (requireMergedSpec)
                {
                    var specPr = await ReleasePlanSpecHelper.GetPullRequestAsync(githubService, releasePlan.ActiveSpecPullRequest, ct);
                    if (!specPr.Merged || !string.Equals(specPr.MergeCommitSha, specCommitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        response.Status = "Failed";
                        response.ResponseErrors.Add("Auto-release generation requires the confirmed target to use the linked PR's merge commit. Update the release target after merge. Pre-merge draft SDK review remains available without --require-merged-spec.");
                        return response;
                    }
                }

                string sdkRepoBranch = "";                
                var sdkPullRequestUrl = sdkInfo?.SdkPullRequestUrl;
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    var parsedUrl = DevOpsService.ParseSDKPullRequestUrl(sdkPullRequestUrl);
                    var sdkPullRequest = await githubService.GetPullRequestAsync(parsedUrl.RepoOwner, parsedUrl.RepoName, parsedUrl.PrNumber, ct);
                    if (sdkPullRequest is not null && sdkPullRequest.State != "closed" && sdkPullRequest.Merged == false)
                    {
                        sdkRepoBranch = sdkPullRequest.Head.Ref;
                    }
                }

                logger.LogInformation("Running SDK generation pipeline");
                ct.ThrowIfCancellationRequested();
                var pipelineRun = await devopsService.RunSDKGenerationPipelineAsync(specCommitSha, typeSpecProjectPath, apiVersion, sdkReleaseType, language, workItemId, sdkRepoBranch, requireMergedSpec, ct);
                response.Status = "Success";
                response.Details.Add($"SDK generation uses pinned spec commit {specCommitSha} and API version '{apiVersion}'.");
                response.Details.Add($"Azure DevOps pipeline {DevOpsService.GetPipelineUrl(pipelineRun.Id)} has been initiated to generate the SDK. Build ID is {pipelineRun.Id}. Once the pipeline job completes, an SDK pull request for {language} will be created.");
                return response;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorResponse = new ReleaseWorkflowResponse();
                errorResponse.ResponseError = $"Failed to run pipeline to generate SDK, Details: {ex.Message}";
                errorResponse.Status = "Failed";
                errorResponse.ExitCode = 1;
                errorResponse.SetLanguage(language);
                errorResponse.TypeSpecProject = typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot);
                return errorResponse;
            }
        }

        private static bool TryGetGenerationPipelineBuildId(string pipelineUrl, out int buildId)
        {
            buildId = 0;
            // SDK generation runs are queued in the internal project. Do not interpret a link
            // from another organization or project as one of those builds.
            var expectedPath = new Uri(DevOpsService.GetPipelineUrl(0)).GetLeftPart(UriPartial.Path);
            return Uri.TryCreate(pipelineUrl, UriKind.Absolute, out var uri) &&
                string.Equals(uri.GetLeftPart(UriPartial.Path), expectedPath, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(HttpUtility.ParseQueryString(uri.Query)["buildId"], out buildId) && buildId > 0;
        }

        /// <summary>
        /// Get SDK pull request link from SDK generation pipeline.
        /// </summary>
        /// <param name="language">SDK Language</param>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <param name="workItemId">Work item ID for the release plan</param>
        /// <returns></returns>
        [McpServerTool(Name = GetSdkPullRequestLinkToolName), Description("Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item.")]
        public async Task<ReleaseWorkflowResponse> GetSDKPullRequestDetails(string language, int workItemId, int buildId = 0, CancellationToken ct = default)
        {
            try
            {
                var response = new ReleaseWorkflowResponse();
                language = inputSanitizer.SanitizeLanguage(language);
                response.SetLanguage(language);
                if (!IsValidLanguage(language))
                {
                    response.ResponseError = $"Unsupported language to get pull request details. Supported languages: {string.Join(", ", SUPPORTED_LANGUAGES)}";
                    return response;
                }

                if (buildId == 0 && workItemId == 0)
                {
                    response.ResponseError = "Either build ID or release plan work item ID is required to get SDK pull request details.";
                    return response;
                }

                // Get SDK details from work item
                if (buildId == 0)
                {
                    response.Details.Add("Build Id is not available. Checking for SDK pull request details in release plan work item.");
                    var releasePlan = await devopsService.ResolveReleasePlanByIdAsync(workItemId, ct);
                    var sdkInfo = releasePlan?.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));
                    if (sdkInfo != null && !string.IsNullOrEmpty(sdkInfo.SdkPullRequestUrl))
                    {
                        response.Details.Add($"SDK pull request details for {language}: {sdkInfo.SdkPullRequestUrl}");
                        return response;
                    }
                    else
                    {
                        response.ResponseError = $"No SDK pull request details found for {language} in release plan work item.";
                        return response;
                    }
                }

                // Find SDK details from build pipeline run
                var pipeline = await devopsService.GetPipelineRunAsync(buildId, ct);
                if (pipeline == null)
                {
                    response.ResponseError = $"Failed to get SDK generation pipeline run with build ID {buildId}";
                    return response;
                }

                if (pipeline.Status != BuildStatus.Completed)
                {
                    response.Details.Add($"SDK generation pipeline is not in completed status to get generated SDK pull request details, Status: {pipeline.Status}. For more details: {DevOpsService.GetPipelineUrl(buildId)}");
                    return response;
                }

                if (pipeline.Result != BuildResult.Succeeded && pipeline.Result != BuildResult.PartiallySucceeded)
                {
                    response.ResponseError = $"SDK generation pipeline did not succeed. Status: {pipeline.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                    return response;
                }

                var pr = await devopsService.GetSDKPullRequestFromPipelineRunAsync(buildId, language, workItemId, ct);
                response.Details.Add(pr != null ?
                    $"SDK pull request details for {language}: {pr}" :
                    $"No SDK pull request was created for {language} from SDK generation pipeline run with build ID {buildId}.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get SDK pull request details from SDK generation pipeline");
                return new() { ResponseError = $"Failed to get pull request details from SDK generation pipeline, Error: {ex.Message}" };
            }
        }

        public static bool IsValidLanguage(string language)
        {
            return SUPPORTED_LANGUAGES.Contains(language.ToLower());
        }
    }
}
