// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Globalization;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("Tool to update SDK package release status in the release plan")]
    [McpServerToolType]
    public class PackageReleaseStatusTool(
        IDevOpsService devOpsService,
        ILogger<PackageReleaseStatusTool> logger,
        INotificationService notificationService
    ) : MCPTool
    {
        private const int ReleasePlanAutomationPipelineDefinitionId = 8254;

        public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.ReleasePlan];

        // Commands
        private const string updateReleaseStatusCommandName = "update-release-status";

        // Options
        private readonly Option<string> packageNameOpt = new("--package-name", "-p")
        {
            Description = "SDK package name",
            Required = true,
        };

        private readonly Option<int> releasePlanIdOpt = new("--release-plan-id")
        {
            Description = "Release plan ID supplied for a manual release (not the Azure DevOps work item ID). Takes priority over --sdk-pull-request. When neither is supplied, the transitional package-name lookup is used.",
            Required = false,
        };

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "SDK language (e.g., .NET, Java, JavaScript, Python, Go)",
            Required = true,
        };

        private readonly Option<string?> sdkReleaseTypeOpt = new("--sdk-release-type")
        {
            Description = "SDK release type (e.g., beta, stable). Used only by the transitional package-name lookup when neither a plan ID nor an SDK PR is supplied.",
            Required = false,
        };

        private readonly Option<string?> sdkPullRequestOpt = new("--sdk-pull-request")
        {
            Description = "Full URL of the SDK PR that triggered an automatic release. Without a plan ID, find exactly one in-progress ADO plan linked to this PR. Invalid or unlinked PRs never fall back to the package-name lookup.",
            Required = false,
        };

        private readonly Option<string?> releasePipelineOpt = new("--release-pipeline")
        {
            Description = "Release pipeline URL.",
            Required = false,
        };

        private readonly Option<string> releaseStatusOpt = new("--status", "-s")
        {
            Description = "Release status (e.g., Released, Pending)",
            Required = false,
            DefaultValueFactory = _ => "Released"
        };

        private readonly Option<string?> packageVersionOpt = new("--package-version")
        {
            Description = "SDK package version being released",
            Required = false,
        };

        protected override Command GetCommand() =>
            new McpCommand(updateReleaseStatusCommandName, "Update package release status in the release plan")
            {
                packageNameOpt, languageOpt, releaseStatusOpt, packageVersionOpt, releasePlanIdOpt, sdkReleaseTypeOpt, sdkPullRequestOpt, releasePipelineOpt
            };


        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var commandParser = parseResult;
            var command = commandParser.CommandResult.Command.Name;
            switch (command)
            {
                case updateReleaseStatusCommandName:
                    var packageName = commandParser.GetValue(packageNameOpt);
                    var language = commandParser.GetValue(languageOpt);
                    var releaseStatus = commandParser.GetValue(releaseStatusOpt);
                    var packageVersion = commandParser.GetValue(packageVersionOpt);
                    var releasePlanId = commandParser.GetValue(releasePlanIdOpt);
                    var sdkReleaseType = commandParser.GetValue(sdkReleaseTypeOpt);
                    var releasePipelineUrl = commandParser.GetValue(releasePipelineOpt);
                    var sdkPullRequest = commandParser.GetValue(sdkPullRequestOpt);
                    return await UpdatePackageReleaseStatus(packageName, language, releaseStatus, packageVersion, releasePlanId, sdkReleaseType, releasePipelineUrl, sdkPullRequest, ct);

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        /// <summary>
        /// Updates only the SDK entry explicitly correlated with this package release.
        /// </summary>
        /// <param name="packageName">The name of the package.</param>
        /// <param name="language">The language of the package.</param>
        /// <param name="releaseStatus">The release status to set (e.g., Released, Pending).</param>
        /// <param name="packageVersion">The version of the package.</param>
        /// <param name="releasePlanId">The user-facing release plan ID propagated with this release, not a work item ID.</param>
        /// <param name="sdkReleaseType">The SDK release type (e.g., beta, stable).</param>
        /// <param name="sdkPullRequest">The URL of the SDK pull request associated with the release.</param>
        /// <param name="releasePipelineUrl">The URL of the release pipeline.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response of the update operation.</returns>
        public async Task<ReleaseStatusUpdateResponse> UpdatePackageReleaseStatus(string packageName, string language, string releaseStatus, string? packageVersion, int releasePlanId = 0, string? sdkReleaseType = null, string? releasePipelineUrl = null, string? sdkPullRequest = null, CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                // Validate inputs
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    return new ReleaseStatusUpdateResponse { ResponseError = "Package name cannot be null or empty." };
                }
                if (string.IsNullOrWhiteSpace(language))
                {
                    return new ReleaseStatusUpdateResponse { PackageName = packageName, ResponseError = "Language cannot be null or empty." };
                }

                var response = new ReleaseStatusUpdateResponse()
                {
                    PackageName = packageName,
                    Language = SdkLanguageHelpers.GetSdkLanguage(language.Trim()),
                    ReleasePlanId = releasePlanId,
                    PackageVersion = packageVersion,
                    ReleasePipelineUrl = releasePipelineUrl,
                    SdkReleaseType = sdkReleaseType,
                    SdkPullRequest = sdkPullRequest
                };

                ReleaseStatusUpdateResponse Reject(string reason)
                {
                    response.ResponseError = $"No release plan updated for ID {releasePlanId}, SDK PR '{sdkPullRequest}', language '{language}', package '{packageName}'. {reason}";
                    return response;
                }

                if (response.Language is not (SdkLanguage.DotNet or SdkLanguage.Java or SdkLanguage.JavaScript or SdkLanguage.Python or SdkLanguage.Go))
                {
                    response.Message = $"Language '{language}' is not supported. Supported languages: {string.Join(", ", ReleasePlanTool.SUPPORTED_LANGUAGES)}";
                    return response;
                }
                if (releasePlanId < 0)
                {
                    return Reject("The release-plan ID must be a positive integer.");
                }
                if (string.IsNullOrWhiteSpace(releaseStatus))
                {
                    return Reject("Release status cannot be empty.");
                }

                // Only callers that supply neither correlation input use the transitional lookup.
                if (releasePlanId == 0 && string.IsNullOrWhiteSpace(sdkPullRequest))
                {
                    return await UpdateByLegacyPackageLookupAsync(response, packageName, language, releaseStatus, packageVersion, sdkReleaseType, releasePipelineUrl, sdkPullRequest, "No release-plan ID or SDK PR was supplied.", ct);
                }
                if (!string.IsNullOrWhiteSpace(sdkPullRequest))
                {
                    sdkPullRequest = DevOpsService.NormalizeSdkPullRequestUrl(sdkPullRequest, response.Language.ToWorkItemString());
                    response.SdkPullRequest = sdkPullRequest;
                }

                bool isAgentTesting = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var result) && result;
                var lookupBySdkPr = releasePlanId == 0;
                ReleasePlanWorkItem releasePlan;
                if (lookupBySdkPr)
                {
                    var releasePlans = await devOpsService.GetReleasePlansBySdkPullRequestAsync(sdkPullRequest!, response.Language.ToWorkItemString(), isAgentTesting, ct);
                    if (releasePlans.Count != 1)
                    {
                        return Reject($"Expected exactly one release plan; found {releasePlans.Count}. Candidate work item IDs: {string.Join(", ", releasePlans.Select(p => p.WorkItemId))}.");
                    }
                    releasePlan = releasePlans[0];
                }
                else
                {
                    releasePlan = await devOpsService.GetReleasePlanAsync(releasePlanId, ct);
                }

                if (releasePlan.ReleasePlanId <= 0 || (!lookupBySdkPr && releasePlan.ReleasePlanId != releasePlanId) ||
                    releasePlan.WorkItemId <= 0 || releasePlan.IsTestReleasePlan != isAgentTesting)
                {
                    return Reject("The resolved work item must have a valid release-plan ID and match the supplied ID and environment.");
                }
                releasePlanId = releasePlan.ReleasePlanId;
                response.ReleasePlanId = releasePlanId;
                var sdkEntries = releasePlan.SDKInfo.Where(s => SdkLanguageHelpers.GetSdkLanguage(s.Language) == response.Language).ToList();
                if (sdkEntries.Count != 1 || !string.Equals(sdkEntries[0].PackageName, packageName, StringComparison.Ordinal))
                {
                    return Reject("The plan must contain exactly one entry for this language, with the exact package name. No other language or package is selected.");
                }
                var sdkInfo = sdkEntries[0];
                if (!string.IsNullOrWhiteSpace(sdkPullRequest) && !string.Equals(sdkInfo.SdkPullRequestUrl, sdkPullRequest, StringComparison.OrdinalIgnoreCase))
                {
                    return Reject("The SDK pull request does not match this language/package entry.");
                }
                var isInProgress = string.Equals(releasePlan.Status, "In Progress", StringComparison.OrdinalIgnoreCase);

                // Retries cannot overwrite a recorded release, even after the overall plan has finished.
                if (string.Equals(sdkInfo.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(releaseStatus, "Released", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(packageVersion) && !string.IsNullOrWhiteSpace(sdkInfo.ReleasedVersion) &&
                         !string.Equals(packageVersion, sdkInfo.ReleasedVersion, StringComparison.Ordinal)))
                    {
                        return Reject($"This SDK is already released with version '{sdkInfo.ReleasedVersion}'; its recorded release cannot be overwritten.");
                    }
                    response.ReleaseStatus = sdkInfo.ReleaseStatus;
                    response.Message = "This SDK is already marked Released; its recorded release fields were not changed.";
                    if (isInProgress)
                    {
                        await TryFinishReleasePlanAsync(releasePlan, response, ct);
                    }
                    return response;
                }
                if (!isInProgress || releasePlan.Revision <= 0)
                {
                    // WIQL selection and work-item retrieval are separate reads; the state can change between them.
                    return Reject("The plan must be in progress and have a valid work item revision before updating status.");
                }

                response.TypeSpecProject = releasePlan.APISpecProjectPath;
                logger.LogInformation("Updating release status to {releaseStatus} for package {packageName} in release plan work item {workItemId}", releaseStatus, packageName, releasePlan.WorkItemId);

                // Update the release status for the specific language
                var languageId = DevOpsService.MapLanguageToId(response.Language.ToWorkItemString());
                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { $"Custom.ReleaseStatusFor{languageId}", releaseStatus }
                };

                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    fieldsToUpdate[$"Custom.ReleasedVersionFor{languageId}"] = packageVersion;
                }

                if (!string.IsNullOrWhiteSpace(releasePipelineUrl))
                {
                    fieldsToUpdate[$"Custom.ReleasePipelineFor{languageId}"] = releasePipelineUrl;
                }

                ct.ThrowIfCancellationRequested();
                await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate, releasePlan.Revision, ct);
                logger.LogInformation("Successfully updated release status to {releaseStatus} for package {packageName} in release plan {workItemId}", releaseStatus, packageName, releasePlan.WorkItemId);
                response.ReleaseStatus = releaseStatus;

                // Check if the release plan can be marked as Finished
                if (string.Equals(releaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                {
                    await TryFinishReleasePlanAsync(releasePlan, response, ct);
                }

                return response;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release status for package {packageName}", packageName);
                return new ReleaseStatusUpdateResponse { PackageName = packageName, ReleasePlanId = releasePlanId, SdkPullRequest = sdkPullRequest, ResponseError = $"Failed to update release status for plan {releasePlanId}, SDK PR '{sdkPullRequest}', language '{language}', package '{packageName}': {ex.Message}" };
            }
        }

        // Transitional: keeps the original package-name lookup until every release pipeline forwards a plan ID or SDK PR.
        private async Task<ReleaseStatusUpdateResponse> UpdateByLegacyPackageLookupAsync(ReleaseStatusUpdateResponse response, string packageName, string language, string releaseStatus, string? packageVersion, string? sdkReleaseType, string? releasePipelineUrl, string? sdkPullRequest, string reason, CancellationToken ct)
        {
            logger.LogWarning("LEGACY_RELEASE_PLAN_LOOKUP: {reason} Using the transitional package-name lookup for package {packageName} in {language}.", reason, packageName, language);
            response.ReleaseStatus = releaseStatus;

            logger.LogInformation("Searching for in-progress release plans with package {packageName} for {language}", packageName, language);
            bool isAgentTesting = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var result) && result;
            var releasePlans = await devOpsService.GetReleasePlansForPackageAsync(packageName, language, isAgentTesting, ct);
            if (releasePlans.Count == 0)
            {
                response.Message = $"No in-progress release plans found for package '{packageName}' in language '{language}'.";
                return response;
            }

            var releasePlan = SelectReleasePlan(releasePlans, packageName, sdkReleaseType, sdkPullRequest);
            response.ReleasePlanId = releasePlan.ReleasePlanId;
            response.TypeSpecProject = releasePlan.APISpecProjectPath;
            logger.LogInformation("Updating release status to {releaseStatus} for package {packageName} in release plan work item {workItemId}", releaseStatus, packageName, releasePlan.WorkItemId);

            var languageId = DevOpsService.MapLanguageToId(language);
            var fieldsToUpdate = new Dictionary<string, string>
            {
                { $"Custom.ReleaseStatusFor{languageId}", releaseStatus }
            };

            if (!string.IsNullOrWhiteSpace(packageVersion))
            {
                fieldsToUpdate[$"Custom.ReleasedVersionFor{languageId}"] = packageVersion;
            }

            if (!string.IsNullOrWhiteSpace(releasePipelineUrl))
            {
                fieldsToUpdate[$"Custom.ReleasePipelineFor{languageId}"] = releasePipelineUrl;
            }

            await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate, ct);
            logger.LogInformation("Successfully updated release status to {releaseStatus} for package {packageName} in release plan {workItemId}", releaseStatus, packageName, releasePlan.WorkItemId);

            if (string.Equals(releaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
            {
                var currentLanguageName = DevOpsService.MapLanguageIdToName(languageId);
                var sdkInfo = releasePlan.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, currentLanguageName, StringComparison.OrdinalIgnoreCase));
                if (sdkInfo != null)
                {
                    sdkInfo.ReleaseStatus = releaseStatus;
                }

                if (IsReleasePlanComplete(releasePlan))
                {
                    try
                    {
                        logger.LogInformation("All required languages are complete for release plan {workItemId}. Marking as Finished.", releasePlan.WorkItemId);
                        await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, new Dictionary<string, string>
                        {
                            { "System.State", "Finished" }
                        }, ct);
                        response.ReleasePlanFinished = true;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to mark release plan {workItemId} as Finished", releasePlan.WorkItemId);
                        response.Message = "Release status updated successfully but failed to auto-finish the release plan.";
                    }

                    if (response.ReleasePlanFinished)
                    {
                        await QueueNextReleasePlanAsync(releasePlan, response, ct);
                    }
                }
            }

            return response;
        }

        private ReleasePlanWorkItem SelectReleasePlan(List<ReleasePlanWorkItem> releasePlans, string packageName, string? sdkReleaseType, string? sdkPullRequest)
        {
            var releasePlan = releasePlans[0];
            if (releasePlans.Count > 1)
            {
                logger.LogInformation("Multiple active release plans are found for '{packageName}'", packageName);
                // If an SDK pull request URL is provided, try to select the release plan that matches it.
                if (!string.IsNullOrWhiteSpace(sdkPullRequest))
                {
                    var releasePlanWithSdkPullRequest = releasePlans.FirstOrDefault(rp =>
                        rp.SDKInfo.Any(s =>
                            string.Equals(s.PackageName, packageName, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(s.SdkPullRequestUrl)
                            && string.Equals(s.SdkPullRequestUrl, sdkPullRequest, StringComparison.OrdinalIgnoreCase)));
                    if (releasePlanWithSdkPullRequest != null)
                    {
                        logger.LogInformation("Selected release plan {releasePlanId} with SDK pull request {sdkPullRequest}.", releasePlanWithSdkPullRequest.ReleasePlanId, sdkPullRequest);
                        releasePlan = releasePlanWithSdkPullRequest;
                        return releasePlan;
                    }
                    logger.LogInformation("No release plan matched the SDK pull request {sdkPullRequest}.", sdkPullRequest);
                }
                // If an SDK release type is provided, try to select the release plan that matches it.
                if (!string.IsNullOrWhiteSpace(sdkReleaseType))
                {
                    var releasePlanWithSdkReleaseType = releasePlans.FirstOrDefault(rp => string.Equals(rp.SDKReleaseType, sdkReleaseType, StringComparison.OrdinalIgnoreCase));
                    if (releasePlanWithSdkReleaseType != null)
                    {
                        logger.LogInformation("Selected release plan {releasePlanId} with SDK release type {sdkReleaseType}.", releasePlanWithSdkReleaseType.ReleasePlanId, sdkReleaseType);
                        releasePlan = releasePlanWithSdkReleaseType;
                        return releasePlan;
                    }
                    logger.LogInformation("No release plan matched the SDK release type {sdkReleaseType}.", sdkReleaseType);
                }
                // If no release plan was selected by SDK pull request or SDK release type, try to select the release plan with a merged pull request.
                var releasePlanWithPrMerged = releasePlans.FirstOrDefault(rp => rp.SDKInfo.Any(s => string.Equals(s.PackageName, packageName, StringComparison.OrdinalIgnoreCase) && s.PullRequestStatus.Equals("Merged")));
                if (releasePlanWithPrMerged != null)
                {
                    logger.LogInformation("Selected first release plan {releasePlanId} with pull request as merged.", releasePlanWithPrMerged.ReleasePlanId);
                    releasePlan = releasePlanWithPrMerged;
                }
                else
                {
                    logger.LogInformation("No release plan with merged pull request status found. Defaulting to first release plan {releasePlanId}.", releasePlan.ReleasePlanId);
                }
            }
            else
            {
                logger.LogInformation("Found release plan work item {workItemId} for package {packageName}", releasePlan.WorkItemId, packageName);
            }
            return releasePlan;
        }

        private async Task QueueNextReleasePlanAsync(ReleasePlanWorkItem finishedReleasePlan, ReleaseStatusUpdateResponse response, CancellationToken ct)
        {
            if (!IsReleasePlanAutomationEnabled(finishedReleasePlan))
            {
                response.Message = "Follow-up SDK generation automation is enabled only for management-plane release plans.";
                return;
            }

            ReleasePlanWorkItem? nextReleasePlan = null;
            try
            {
                if (string.IsNullOrWhiteSpace(finishedReleasePlan.APISpecProjectPath))
                {
                    throw new InvalidOperationException($"Release plan {finishedReleasePlan.ReleasePlanId} has no TypeSpec project path. Correct its metadata.");
                }

                var activeReleasePlans = await devOpsService.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    finishedReleasePlan.APISpecProjectPath,
                    ct: ct);

                nextReleasePlan = SelectNextReleasePlan(finishedReleasePlan, activeReleasePlans, response);
                if (nextReleasePlan == null)
                {
                    response.Message = response.Warnings?.Count > 0
                        ? "No eligible newer release plan with valid metadata was found. Review the warnings and correct the release plan metadata."
                        : "No newer release plan eligible for SDK generation automation was found.";
                    logger.LogInformation(
                        "No newer release plan eligible for automation was found after release plan {releasePlanId}.",
                        finishedReleasePlan.ReleasePlanId);
                    return;
                }

                await devOpsService.EnsureReleasePlanAutomationRelationAsync(
                    nextReleasePlan.WorkItemId, finishedReleasePlan.WorkItemId, ct);

                var pipelineRun = await devOpsService.RunPipelineAsync(
                    ReleasePlanAutomationPipelineDefinitionId,
                    new Dictionary<string, string>
                    {
                        ["ReleasePlanId"] = nextReleasePlan.ReleasePlanId.ToString(CultureInfo.InvariantCulture)
                    },
                    ct: ct);

                response.ReleasePlanAutomationTriggered = true;
                response.QueuedReleasePlanId = nextReleasePlan.ReleasePlanId;
                response.ReleasePlanAutomationPipelineUrl = DevOpsService.GetPipelineUrl(pipelineRun.Id);
                logger.LogInformation(
                    "Queued release plan automation pipeline {pipelineUrl} for release plan {releasePlanId} after release plan {finishedReleasePlanId} finished.",
                    response.ReleasePlanAutomationPipelineUrl,
                    nextReleasePlan.ReleasePlanId,
                    finishedReleasePlan.ReleasePlanId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Release plan {releasePlanId} was marked as Finished, but the next release plan automation pipeline could not be queued.",
                    finishedReleasePlan.ReleasePlanId);
                response.Message = $"Release plan is Finished, but the next release plan automation pipeline could not be queued: {ex.Message}";
                response.NextSteps =
                [
                    nextReleasePlan == null
                        ? "Review the release plan metadata and use the azsdk agent to identify the pending release plan and generate its SDK."
                        : $"Use the azsdk agent to generate SDKs for release plan {nextReleasePlan.ReleasePlanId}. More information is available on the release plan dashboard: {nextReleasePlan.ReleasePlanLink}"
                ];
            }

            if (nextReleasePlan == null)
            {
                return;
            }

            try
            {
                var notificationResult = await notificationService.SendEmailNotificationAsync(
                    new ReleasePlanSdkGenerationEmail(finishedReleasePlan, nextReleasePlan, response.ReleasePlanAutomationTriggered), ct);
                if (notificationResult.IsFailure)
                {
                    logger.LogWarning(
                        "Failed to send the SDK generation automation notification for release plan {releasePlanId}: {error}",
                        nextReleasePlan.ReleasePlanId,
                        notificationResult.ErrorMessage);
                    (response.Warnings ??= []).Add(
                        $"The SDK generation notification failed: {notificationResult.ErrorMessage} Check the release plan dashboard for status and next steps.");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send the SDK generation automation notification for release plan {releasePlanId}.", nextReleasePlan.ReleasePlanId);
                (response.Warnings ??= []).Add($"The SDK generation notification failed: {ex.Message} Check the release plan dashboard for status and next steps.");
            }
        }

        internal static bool IsReleasePlanAutomationEnabled(ReleasePlanWorkItem releasePlan)
        {
            return releasePlan.IsManagementPlane;
        }

        private ReleasePlanWorkItem? SelectNextReleasePlan(
            ReleasePlanWorkItem finishedReleasePlan,
            IEnumerable<ReleasePlanWorkItem> activeReleasePlans,
            ReleaseStatusUpdateResponse response)
        {
            if (!TryParseApiVersion(finishedReleasePlan.SpecAPIVersion, out var finishedVersion))
            {
                throw new InvalidOperationException(
                    $"Release plan {finishedReleasePlan.ReleasePlanId} has missing or invalid API version '{finishedReleasePlan.SpecAPIVersion}'. Expected YYYY-MM-DD or YYYY-MM-DD-preview. Correct its metadata.");
            }

            ReleasePlanWorkItem? nextReleasePlan = null;
            ApiVersion? nextVersion = null;
            foreach (var candidate in activeReleasePlans.Where(candidate =>
                candidate.WorkItemId != finishedReleasePlan.WorkItemId
                && string.Equals(candidate.Status, "In Progress", StringComparison.OrdinalIgnoreCase)
                && candidate.ApiReleaseType is ApiReleaseType.PublicPreview or ApiReleaseType.GA
                && candidate.ReleasePlanId > 0))
            {
                if (!TryParseApiVersion(candidate.SpecAPIVersion, out var candidateVersion))
                {
                    logger.LogWarning(
                        "Release plan {releasePlanId} has invalid API version '{apiVersion}' and is not eligible for automation.",
                        candidate.ReleasePlanId,
                        candidate.SpecAPIVersion);
                    (response.Warnings ??= []).Add(
                        $"Skipped release plan {candidate.ReleasePlanId}: missing or invalid API version '{candidate.SpecAPIVersion}'. Expected YYYY-MM-DD or YYYY-MM-DD-preview.");
                    continue;
                }

                if (candidateVersion.CompareTo(finishedVersion) <= 0)
                {
                    continue;
                }

                if (!nextVersion.HasValue
                    || candidateVersion.CompareTo(nextVersion.Value) < 0
                    || (candidateVersion.CompareTo(nextVersion.Value) == 0
                        && candidate.ReleasePlanId < nextReleasePlan!.ReleasePlanId))
                {
                    nextReleasePlan = candidate;
                    nextVersion = candidateVersion;
                }
            }

            return nextReleasePlan;
        }

        private static bool TryParseApiVersion(string? apiVersion, out ApiVersion parsedVersion)
        {
            parsedVersion = default;
            if (string.IsNullOrWhiteSpace(apiVersion))
            {
                return false;
            }

            const string previewSuffix = "-preview";
            var isPreview = apiVersion.EndsWith(previewSuffix, StringComparison.OrdinalIgnoreCase);
            var dateText = isPreview ? apiVersion[..^previewSuffix.Length] : apiVersion;

            if (DateOnly.TryParseExact(
                dateText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                parsedVersion = new ApiVersion(date, isPreview);
                return true;
            }

            return false;
        }

        private readonly record struct ApiVersion(DateOnly Date, bool IsPreview) : IComparable<ApiVersion>
        {
            public int CompareTo(ApiVersion other)
            {
                var dateComparison = Date.CompareTo(other.Date);
                if (dateComparison != 0)
                {
                    return dateComparison;
                }

                return other.IsPreview.CompareTo(IsPreview);
            }
        }

        private async Task TryFinishReleasePlanAsync(ReleasePlanWorkItem releasePlan, ReleaseStatusUpdateResponse response, CancellationToken ct)
        {
            try
            {
                // Share the same fresh-read and revision checks for new releases and completion retries.
                ct.ThrowIfCancellationRequested();
                var currentPlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, ct);
                // The work-item ID fixes identity; only mutable completion metadata and the fresh revision need checking.
                if (currentPlan.IsTestReleasePlan != releasePlan.IsTestReleasePlan || currentPlan.Revision <= 0 ||
                    currentPlan.IsManagementPlane != releasePlan.IsManagementPlane || currentPlan.IsDataPlane != releasePlan.IsDataPlane ||
                    !string.Equals(currentPlan.APISpecProjectPath, releasePlan.APISpecProjectPath, StringComparison.Ordinal) ||
                    currentPlan.SDKInfo.Count != releasePlan.SDKInfo.Count ||
                    currentPlan.SDKInfo.Any(current => !releasePlan.SDKInfo.Any(original =>
                        string.Equals(current.Language, original.Language, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(current.PackageName, original.PackageName, StringComparison.Ordinal))))
                {
                    response.Message = "The SDK is marked Released, but the plan changed before completion could be checked. No completion update was made.";
                }
                else if (string.Equals(currentPlan.Status, "In Progress", StringComparison.OrdinalIgnoreCase) && IsReleasePlanComplete(currentPlan))
                {
                    logger.LogInformation("All required languages are complete for release plan {workItemId}. Marking as Finished.", releasePlan.WorkItemId);
                    ct.ThrowIfCancellationRequested();
                    await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, new Dictionary<string, string>
                    {
                        { "System.State", "Finished" }
                    }, currentPlan.Revision, ct);
                    response.ReleasePlanFinished = true;
                    await QueueNextReleasePlanAsync(currentPlan, response, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to mark release plan {workItemId} as Finished", releasePlan.WorkItemId);
                response.Message = "The SDK is marked Released but failed to auto-finish the release plan.";
            }
        }

        internal static bool IsReleasePlanComplete(ReleasePlanWorkItem releasePlan)
        {
            var requiredLanguages = releasePlan.IsManagementPlane
                ? ReleasePlanTool.languagesforMgmtplane
                : ReleasePlanTool.languagesforDataplane;

            var sdkInfoByLanguage = releasePlan.SDKInfo.ToDictionary(i => i.Language, StringComparer.OrdinalIgnoreCase);

            return requiredLanguages.All(lang =>
                sdkInfoByLanguage.TryGetValue(lang, out var info)
                && (string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
