// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
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

        private readonly Option<string> languageOpt = new("--language", "-l")
        {
            Description = "SDK language (e.g., .NET, Java, JavaScript, Python, Go)",
            Required = true,
        };

        private readonly Option<string> releaseStatusOpt = new("--status", "-s")
        {
            Description = "Release status (e.g., Released, Pending)",
            Required = false,
            DefaultValueFactory = _ => "Released"
        };

        protected override Command GetCommand() =>
            new McpCommand(updateReleaseStatusCommandName, "Update package release status in the release plan")
            {
                packageNameOpt, languageOpt, releaseStatusOpt
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
                    return await UpdatePackageReleaseStatus(packageName, language, releaseStatus);

                default:
                    logger.LogError("Unknown command: {command}", command);
                    return new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" };
            }
        }

        public async Task<ReleaseStatusUpdateResponse> UpdatePackageReleaseStatus(string packageName, string language, string releaseStatus)
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
                    ReleaseStatus = releaseStatus
                };

                if (!ReleasePlanTool.SUPPORTED_LANGUAGES.Contains(language.ToLower()))
                {
                    response.ResponseError = $"Language '{language}' is not supported. Supported languages: {string.Join(", ", ReleasePlanTool.SUPPORTED_LANGUAGES)}";
                    return response;
                }
                
                logger.LogInformation("Searching for in-progress release plans with package {packageName} for {language}", packageName, language);
                bool isAgentTesting = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var result) && result;
                // Find all release plans in "In Progress" status with the given package name
                var releasePlans = await devOpsService.GetReleasePlansForPackageAsync(packageName, language, isAgentTesting);
                if (releasePlans.Count == 0)
                {
                    response.ResponseError = $"No in-progress release plans found for package '{packageName}' in language '{language}'.";
                    return response;
                }

                // If there are multiple release plans, prioritize the one with a merged pull request for the package
                var releasePlan = releasePlans[0];
                if (releasePlans.Count > 1)
                {
                    logger.LogInformation("Multiple active release plans are found for '{packageName}' in language '{language}'", packageName, language);
                    var releasePlanWithPrMerged = releasePlans.FirstOrDefault(rp => rp.SDKInfo.Any(s => s.PackageName.Equals(packageName) && s.PullRequestStatus.Equals("Merged")));
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
                    logger.LogInformation("Found release plan work item {workItemId} for package {packageName} in language {language}", releasePlan.WorkItemId, packageName, language);
                }

                response.ReleasePlanId = releasePlan.ReleasePlanId;
                response.TypeSpecProject = releasePlan.APISpecProjectPath;
                logger.LogInformation("Updating release status for package {packageName} in release plan work item {workItemId}", packageName, releasePlan.WorkItemId);

                // Update the release status for the specific language
                var fieldsToUpdate = new Dictionary<string, string>
                {
                    { $"Custom.ReleaseStatusFor{DevOpsService.MapLanguageToId(language)}", releaseStatus }
                };

                await devOpsService.UpdateWorkItemAsync(releasePlan.WorkItemId, fieldsToUpdate);
                logger.LogInformation("Successfully updated release status for package {packageName} in release plan {workItemId}", packageName, releasePlan.WorkItemId);
                response.ReleaseStatus = releaseStatus;
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update release status for package {packageName}", packageName);
                return new ReleaseStatusUpdateResponse { PackageName = packageName, ResponseError = $"Failed to update release status: {ex.Message}" };
            }
        }
    }
}
