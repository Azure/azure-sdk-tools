// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("Tool to update SDK package release status in the release plan")]
    [McpServerToolType]
    public class PackageReleaseStatusTool(
        IDevOpsService devOpsService,
        ILogger<PackageReleaseStatusTool> logger
    ) : MCPTool
    {
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
            Description = "Release plan ID associated with this package release (not the Azure DevOps work item ID). No plan is updated when omitted.",
            Required = false,
        };

        private readonly Option<string?> apiVersionOpt = new("--api-version")
        {
            Description = "Single spec API version from the package being released. Required when a release plan ID is provided; must match the plan exactly.",
            Required = false,
        };

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "SDK language (e.g., .NET, Java, JavaScript, Python, Go)",
            Required = true,
        };

        private readonly Option<string?> sdkReleaseTypeOpt = new("--sdk-release-type")
        {
            Description = "SDK release type (e.g., beta, stable). When supplied, must match the identified plan; never used to select another plan.",
            Required = false,
        };

        private readonly Option<string?> sdkPullRequestOpt = new("--sdk-pull-request")
        {
            Description = "SDK pull request URL. When supplied, must match the identified language/package entry.",
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
                packageNameOpt, languageOpt, releaseStatusOpt, packageVersionOpt, releasePlanIdOpt, apiVersionOpt, sdkReleaseTypeOpt, sdkPullRequestOpt, releasePipelineOpt
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
                    var apiVersion = commandParser.GetValue(apiVersionOpt);
                    return await UpdatePackageReleaseStatus(packageName, language, releaseStatus, packageVersion, releasePlanId, sdkReleaseType, releasePipelineUrl, sdkPullRequest, apiVersion, ct);

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
        /// <param name="apiVersion">The single spec API version from the package being released.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response of the update operation.</returns>
        public async Task<ReleaseStatusUpdateResponse> UpdatePackageReleaseStatus(string packageName, string language, string releaseStatus, string? packageVersion, int releasePlanId = 0, string? sdkReleaseType = null, string? releasePipelineUrl = null, string? sdkPullRequest = null, string? apiVersion = null, CancellationToken ct = default)
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
                    ApiVersion = apiVersion,
                    PackageVersion = packageVersion,
                    ReleasePipelineUrl = releasePipelineUrl,
                    SdkReleaseType = sdkReleaseType,
                    SdkPullRequest = sdkPullRequest
                };

                ReleaseStatusUpdateResponse Reject(string reason)
                {
                    response.ResponseError = $"No release plan updated for ID {releasePlanId}, language '{language}', package '{packageName}', API version '{apiVersion}'. {reason}";
                    return response;
                }

                if (response.Language is not (SdkLanguage.DotNet or SdkLanguage.Java or SdkLanguage.JavaScript or SdkLanguage.Python or SdkLanguage.Go))
                {
                    return Reject($"Language '{language}' is not supported. Supported languages: .NET, Java, JavaScript, Python, Go.");
                }
                if (releasePlanId == 0)
                {
                    response.Message = "No release-plan ID was supplied; no release plan was updated. Independent SDK releases do not require a release plan.";
                    return response;
                }
                if (releasePlanId < 0)
                {
                    return Reject("The release-plan ID must be a positive integer.");
                }
                if (!IsSingleApiVersion(apiVersion))
                {
                    return Reject("Provide one explicit API version from this package's metadata, not a list, default, or latest selector.");
                }
                if (string.IsNullOrWhiteSpace(releaseStatus))
                {
                    return Reject("Release status cannot be empty.");
                }

                bool isAgentTesting = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var result) && result;
                var releasePlans = await devOpsService.GetReleasePlansByIdAsync(releasePlanId, isAgentTesting, ct);
                if (releasePlans.Count != 1)
                {
                    return Reject($"Expected exactly one release plan with this ID; found {releasePlans.Count}. Candidate work item IDs: {string.Join(", ", releasePlans.Select(p => p.WorkItemId))}.");
                }

                var releasePlan = releasePlans[0];
                if (releasePlan.ReleasePlanId != releasePlanId || releasePlan.WorkItemId <= 0 || releasePlan.IsTestReleasePlan != isAgentTesting)
                {
                    return Reject("The resolved work item does not match the supplied release-plan ID and environment.");
                }
                var sdkEntries = releasePlan.SDKInfo.Where(s => SdkLanguageHelpers.GetSdkLanguage(s.Language) == response.Language).ToList();
                if (sdkEntries.Count != 1 || !string.Equals(sdkEntries[0].PackageName, packageName, StringComparison.Ordinal))
                {
                    return Reject("The plan must contain exactly one entry for this language, with the exact package name. No other language or package is selected.");
                }
                var sdkInfo = sdkEntries[0];
                if (!string.Equals(releasePlan.SpecAPIVersion, apiVersion, StringComparison.Ordinal))
                {
                    return Reject($"The package API version does not match the plan API version '{releasePlan.SpecAPIVersion}'.");
                }
                if (!string.IsNullOrWhiteSpace(sdkReleaseType) && !string.Equals(releasePlan.SDKReleaseType, sdkReleaseType, StringComparison.OrdinalIgnoreCase))
                {
                    return Reject($"SDK release type does not match the plan's '{releasePlan.SDKReleaseType}' target.");
                }
                if (!string.IsNullOrWhiteSpace(sdkPullRequest) && !string.Equals(sdkInfo.SdkPullRequestUrl, sdkPullRequest, StringComparison.OrdinalIgnoreCase))
                {
                    return Reject("The SDK pull request does not match this language/package entry.");
                }
                var isInProgress = string.Equals(releasePlan.Status, "In Progress", StringComparison.OrdinalIgnoreCase);
                var isFinished = string.Equals(releasePlan.Status, "Finished", StringComparison.OrdinalIgnoreCase);
                if (!isInProgress && !isFinished)
                {
                    return Reject($"The plan is not in progress (state '{releasePlan.Status}').");
                }

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
                    response.Message = "This SDK is already marked Released; no release plan changes were made.";
                    return response;
                }
                if (!isInProgress || releasePlan.Revision <= 0)
                {
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
                    try
                    {
                        // Other languages may have completed or the plan may have changed since the first read.
                        var currentPlan = await devOpsService.GetReleasePlanForWorkItemAsync(releasePlan.WorkItemId, ct);
                        if (currentPlan.ReleasePlanId != releasePlanId || currentPlan.WorkItemId != releasePlan.WorkItemId ||
                            currentPlan.IsTestReleasePlan != isAgentTesting || currentPlan.Revision <= 0 ||
                            currentPlan.IsManagementPlane != releasePlan.IsManagementPlane || currentPlan.IsDataPlane != releasePlan.IsDataPlane ||
                            !string.Equals(currentPlan.APISpecProjectPath, releasePlan.APISpecProjectPath, StringComparison.Ordinal) ||
                            !string.Equals(currentPlan.SpecAPIVersion, apiVersion, StringComparison.Ordinal) ||
                            !string.Equals(currentPlan.SDKReleaseType, releasePlan.SDKReleaseType, StringComparison.OrdinalIgnoreCase) ||
                            currentPlan.SDKInfo.Count != releasePlan.SDKInfo.Count ||
                            currentPlan.SDKInfo.Any(current => !releasePlan.SDKInfo.Any(original =>
                                string.Equals(current.Language, original.Language, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(current.PackageName, original.PackageName, StringComparison.Ordinal))))
                        {
                            response.Message = "Release status updated, but the plan changed before completion could be checked. No completion update was made.";
                        }
                        else if (string.Equals(currentPlan.Status, "In Progress", StringComparison.OrdinalIgnoreCase) && IsReleasePlanComplete(currentPlan))
                        {
                            logger.LogInformation("All required languages are complete for release plan {workItemId}. Marking as Finished.", releasePlan.WorkItemId);
                            await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, new Dictionary<string, string>
                            {
                                { "System.State", "Finished" }
                            }, currentPlan.Revision, ct);
                            response.ReleasePlanFinished = true;
                        }
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
                return new ReleaseStatusUpdateResponse { PackageName = packageName, ReleasePlanId = releasePlanId, ApiVersion = apiVersion, ResponseError = $"Failed to update release status for plan {releasePlanId}, language '{language}', package '{packageName}', API version '{apiVersion}': {ex.Message}" };
            }
        }

        private static bool IsSingleApiVersion(string? apiVersion)
        {
            return !string.IsNullOrWhiteSpace(apiVersion) &&
                !string.Equals(apiVersion, "latest", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(apiVersion, "default", StringComparison.OrdinalIgnoreCase) &&
                char.IsAsciiLetterOrDigit(apiVersion[0]) &&
                apiVersion.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
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
