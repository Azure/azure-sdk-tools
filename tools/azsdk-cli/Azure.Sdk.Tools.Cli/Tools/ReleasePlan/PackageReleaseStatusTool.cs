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
            Description = "Optional release plan ID. If provided, it is used as an additional filter when searching by package name to select the correct release plan.",
            Required = false,
        };

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "SDK language (e.g., .NET, Java, JavaScript, Python, Go)",
            Required = true,
        };

        private readonly Option<string?> sdkReleaseTypeOpt = new("--sdk-release-type")
        {
            Description = "SDK release type (e.g., beta, stable)",
            Required = false,
        };

        private readonly Option<string?> sdkPullRequestOpt = new("--sdk-pull-request")
        {
            Description = "SDK pull request URL.",
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
        /// Updates the release status for a given package in the release plan to either Released or Approval pending
        /// </summary>
        /// <param name="packageName">The name of the package.</param>
        /// <param name="language">The language of the package.</param>
        /// <param name="releaseStatus">The release status to set (e.g., Released, Pending).</param>
        /// <param name="packageVersion">The version of the package.</param>
        /// <param name="releasePlanId">The ID of the release plan work item.</param>
        /// <param name="sdkReleaseType">The SDK release type (e.g., beta, stable).</param>
        /// <param name="sdkPullRequest">The URL of the SDK pull request associated with the release.</param>
        /// <param name="releasePipelineUrl">The URL of the release pipeline.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation, containing the response of the update operation.</returns>
        public async Task<ReleaseStatusUpdateResponse> UpdatePackageReleaseStatus(string packageName, string language, string releaseStatus, string? packageVersion, int releasePlanId = 0, string? sdkReleaseType = null, string? releasePipelineUrl = null, string? sdkPullRequest = null, CancellationToken ct = default)
        {
            try
            {
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
                    Language = SdkLanguageHelpers.GetSdkLanguage(language),
                    ReleaseStatus = releaseStatus,
                    PackageVersion = packageVersion,
                    ReleasePipelineUrl = releasePipelineUrl,
                    SdkReleaseType = sdkReleaseType,
                    SdkPullRequest = sdkPullRequest
                };

                if (!ReleasePlanTool.SUPPORTED_LANGUAGES.Contains(language.ToLower()))
                {
                    response.Message = $"Language '{language}' is not supported. Supported languages: {string.Join(", ", ReleasePlanTool.SUPPORTED_LANGUAGES)}";
                    return response;
                }

                logger.LogInformation("Searching for in-progress release plans with package {packageName} for {language}", packageName, language);
                bool isAgentTesting = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var result) && result;
                // Find all release plans in "In Progress" status with the given package name
                var releasePlans = await devOpsService.GetReleasePlansForPackageAsync(packageName, language, isAgentTesting, ct);
                if (releasePlans.Count == 0)
                {
                    response.Message = $"No in-progress release plans found for package '{packageName}' in language '{language}'.";
                    return response;
                }

                ReleasePlanWorkItem releasePlan;

                // If release plan ID is provided, use it to select the matching release plan from the results
                if (releasePlanId > 0)
                {
                    var matchingPlan = releasePlans.FirstOrDefault(rp => rp.ReleasePlanId == releasePlanId);
                    if (matchingPlan != null)
                    {
                        logger.LogInformation("Found release plan {releasePlanId} matching the provided ID for package {packageName}.", releasePlanId, packageName);
                        releasePlan = matchingPlan;
                    }
                    else
                    {
                        response.Message = $"Release plan with ID '{releasePlanId}' not found among in-progress release plans for package '{packageName}' in language '{language}'.";
                        return response;
                    }
                }
                else
                {
                    releasePlan = SelectReleasePlan(releasePlans, packageName, sdkReleaseType, sdkPullRequest);
                }

                response.ReleasePlanId = releasePlan.ReleasePlanId;
                response.TypeSpecProject = releasePlan.APISpecProjectPath;
                logger.LogInformation("Updating release status to {releaseStatus} for package {packageName} in release plan work item {workItemId}", releaseStatus, packageName, releasePlan.WorkItemId);

                // Update the release status for the specific language
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
                response.ReleaseStatus = releaseStatus;

                // Check if the release plan can be marked as Finished
                if (string.Equals(releaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                {
                    // Update in-memory SDKInfo to reflect the new status
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
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to mark release plan {workItemId} as Finished", releasePlan.WorkItemId);
                            response.Message = "Release status updated successfully but failed to auto-finish the release plan.";
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release status for package {packageName}", packageName);
                return new ReleaseStatusUpdateResponse { PackageName = packageName, ResponseError = $"Failed to update release status: {ex.Message}" };
            }
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
                var releasePlanWithPrMerged = releasePlans.FirstOrDefault(rp => rp.SDKInfo.Any(s => s.PullRequestStatus.Equals("Merged")));
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

        internal static bool IsReleasePlanComplete(ReleasePlanWorkItem releasePlan)
        {
            var requiredLanguages = releasePlan.IsManagementPlane
                ? ReleasePlanTool.languagesforMgmtplane
                : ReleasePlanTool.languagesforDataplane;

            var sdkInfoByLanguage = releasePlan.SDKInfo.ToDictionary(i => i.Language, System.StringComparer.OrdinalIgnoreCase);

            return requiredLanguages.All(lang =>
                sdkInfoByLanguage.TryGetValue(lang, out var info)
                && (string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
