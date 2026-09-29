// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Text.RegularExpressions;
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

        // MCP Tool Names
        private const string RunGenerateSdkToolName = "azsdk_run_generate_sdk";
        private const string GetSdkPullRequestLinkToolName = "azsdk_get_sdk_pull_request_link";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "TypeSpec project path; must match the stored project. A matching repository-relative path needs no local clone.",
            Required = true,
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Optional spec pull request number; must match the PR linked to the release plan",
            Required = false,
        };

        private readonly Option<string> apiVersionOpt = new("--api-version")
        {
            Description = "Expected stored API version; cannot override the plan's version",
            Required = false,
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--release-type")
        {
            Description = "SDK release type: beta or stable; must match the stored release type",
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
                typeSpecProjectPathOpt, apiVersionOpt, sdkReleaseTypeOpt, languageOpt, pullRequestNumberOpt, workItemIdOpt,
            },
            new McpCommand(getSdkPullRequestCommandName, "Get SDK pull request link from SDK generation pipeline", GetSdkPullRequestLinkToolName)
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt,
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
                                        ct),
                getSdkPullRequestCommandName => await GetSDKPullRequestDetails(commandParser.GetValue(languageOpt), workItemId: commandParser.GetValue(workItemIdOpt), buildId: commandParser.GetValue(pipelineRunIdOpt), ct: ct),
                _ => new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" },
            };
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
            "Read the plan; pass its repository-relative project path, SDK release type (beta or stable), language and plan/work item ID. Uses stored SpecCommitSHA, SpecAPIVersion and SDK release type for interactive and automated runs. " +
            "Caller inputs only check consistency; they cannot override the stored target. Missing targets must be explicitly configured before generation; there is no fallback to main or the latest spec PR. " +
            "Do not use azsdk_release_sdk (package publishing) or azsdk_get_sdk_pull_request_link (link retrieval) to generate SDKs.")]
        public async Task<ReleaseWorkflowResponse> RunGenerateSdkAsync(string typespecProjectRoot, string sdkReleaseType, string language, int pullRequestNumber = 0, int workItemId = 0, string apiVersion = "", CancellationToken ct = default)
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
                var expectedApiVersion = apiVersion;
                apiVersion = releasePlan.SpecAPIVersion;

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
                    // The stored repository-relative path does not require a local checkout.
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
                sdkReleaseType = sdkReleaseType?.ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(sdkReleaseType) || !validReleaseTypes.Contains(sdkReleaseType))
                {
                    response.ResponseErrors.Add("SDK release type must be set as either beta or stable to generate SDK.");
                    response.Status = "Failed";
                }
                if (!string.Equals(sdkReleaseType, releasePlan.SDKReleaseType, StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add("SDK release type does not match the release plan's stored release type. Explicitly configure the stored target before requesting a different release type.");
                    response.Status = "Failed";
                }

                if (sdkReleaseType.Equals("stable", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(apiVersion) &&
                    !apiVersion.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                    apiVersion.Contains("-preview", StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add($"Stable SDK generation is not allowed from preview API version '{apiVersion}'. Explicitly configure a beta SDK target or a stable API version before generating.");
                    response.Status = "Failed";
                    return response;
                }

                if (!string.IsNullOrWhiteSpace(expectedApiVersion) && !expectedApiVersion.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(apiVersion) && !string.Equals(expectedApiVersion, apiVersion, StringComparison.OrdinalIgnoreCase))
                {
                    response.ResponseErrors.Add($"API version '{expectedApiVersion}' does not match release plan API version '{apiVersion}'. Explicitly configure the stored target instead of overriding this plan.");
                    response.Status = "Failed";
                }
                if (string.IsNullOrWhiteSpace(storedProject) || !string.Equals(typeSpecProjectPath, storedProject, StringComparison.Ordinal))
                {
                    response.ResponseErrors.Add($"TypeSpec project '{typeSpecProjectPath}' does not match the release plan's stored project '{storedProject}'. Explicitly configure the stored project before generating SDKs.");
                    response.Status = "Failed";
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

                var specCommitSha = releasePlan.SpecCommitSHA;
                if (specCommitSha is not { Length: 40 } || !specCommitSha.All(Uri.IsHexDigit) ||
                    string.IsNullOrWhiteSpace(apiVersion) || apiVersion.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add("The release plan has a missing or invalid spec commit SHA or API version. Generation never chooses or saves a target implicitly.");
                    response.NextSteps = ["Explicitly configure the release plan's stored target: a full 40-character hexadecimal Custom.SpecCommitSHA on the parent release plan and the selected API version on its API Spec child, then read the plan and retry."];
                    return response;
                }

                var linkedPullRequest = Regex.Match(releasePlan.ActiveSpecPullRequest ?? string.Empty,
                    @"\Ahttps://github\.com/Azure/azure-rest-api-specs/pull/([1-9][0-9]*)/?\z", RegexOptions.IgnoreCase);
                if (!linkedPullRequest.Success || !int.TryParse(linkedPullRequest.Groups[1].Value, out var linkedPullRequestNumber))
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add("SDK generation requires a linked spec PR in the public Azure/azure-rest-api-specs repository.");
                    return response;
                }
                if (pullRequestNumber > 0 && pullRequestNumber != linkedPullRequestNumber)
                {
                    response.Status = "Failed";
                    response.ResponseErrors.Add($"Spec PR {pullRequestNumber} does not match the release plan's linked PR {linkedPullRequestNumber}. Explicitly configure the stored target before generating from a different spec PR.");
                    return response;
                }

                // A PR lookup classifies release eligibility only; it must never select a new SHA.
                var specPullRequest = await githubService.GetPullRequestAsync("Azure", "azure-rest-api-specs", linkedPullRequestNumber, ct).WaitAsync(ct)
                    ?? throw new InvalidOperationException("The linked spec PR could not be read to determine draft generation behavior.");
                var apiSpecBranchRef = specPullRequest.Merged &&
                    string.Equals(specPullRequest.Base?.Ref, "main", StringComparison.Ordinal) &&
                    string.Equals(specPullRequest.MergeCommitSha, specCommitSha, StringComparison.OrdinalIgnoreCase)
                    ? "refs/heads/main"
                    : $"refs/pull/{linkedPullRequestNumber}/head";

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
                var pipelineRun = await devopsService.RunSDKGenerationPipelineAsync(specCommitSha, typeSpecProjectPath, apiVersion, releasePlan.SDKReleaseType.ToLowerInvariant(), language, workItemId, apiSpecBranchRef, sdkRepoBranch, ct);
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
