// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using System.CommandLine;
using Azure.Sdk.Tools.Cli.Contract;
using System.CommandLine.Invocation;
using System.Text;

namespace Azure.Sdk.Tools.Cli.Tools
{
    [Description("This type contains the MCP tool to run SDK generation using pipeline, check SDK generation pipeline status and to get generated SDK pull request details.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IGitHubService githubService,
        IDevOpsService devopsService,
        IGitHelper gitHelper,
        ITypeSpecHelper typespecHelper,
        IOutputService output,
        ILogger<SpecWorkflowTool> logger) : MCPTool
    {
        private static readonly string PUBLIC_SPECS_REPO = "azure-rest-api-specs";
        private static readonly string REPO_OWNER = "Azure";
        public static readonly string ARM_SIGN_OFF_LABEL = "ARMSignedOff";
        public static readonly string API_STEWARDSHIP_APPROVAL = "APIStewardshipBoard-SignedOff";
        private static readonly string DEFAULT_BRANCH = "main";

        public static readonly HashSet<string> SUPPORTED_LANGUAGES = new()
        {
            "python",
            ".net",
            "javascript",
            "java",
            "go"
        };

        // Commands
        private const string checkApiReadinessCommandName = "check-api-readiness";
        private const string generateSdkCommandName = "generate-sdk";
        private const string getPipelineStatusCommandName = "create-pr";
        private const string getSdkPullRequestCommandName = "get-sdk-pr";
        private const string linkSdkPrCommandName = "link-sdk-pr";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new(["--typespec-project"], "Path to typespec project") { IsRequired = true };
        private readonly Option<int> pullRequestNumberOpt = new(["--pr"], "Pull request number") { IsRequired = false };
        private readonly Option<string> apiVersionOpt = new(["--api-version"], "API version") { IsRequired = true };
        private readonly Option<string> sdkReleaseTypeOpt = new(["--release-type"], "SDK release type: beta or stable") { IsRequired = true };
        private readonly Option<string> languageOpt = new(["--language"], "SDK language, Options[Python, .NET, JavaScript, Java, go]") { IsRequired = true };
        private readonly Option<int> workItemIdOpt = new(["--workitem-id"], "SDK release plan work item id") { IsRequired = false };
        private readonly Option<int> pipelineRunIdOpt = new(["--pipeline-run"], "SDK generation pipeline run id") { IsRequired = true };
        private readonly Option<string> urlOpt = new(["--url"], "Pull request url") { IsRequired = true };
        private readonly Option<int> releasePlanIdOpt = new(["--release-plan"], "SDK release plan id") { IsRequired = false };
        private readonly Option<int> workItemOptionalIdOpt = new(["--workitem-id"], "Release plan work item id") { IsRequired = false };

        private async Task<GenericResponse> IsSdkDetailsPresentInReleasePlanAsync(int workItemId, string language)
        {
            var response = new GenericResponse()
            {
                Status = "Failed"
            };

            try
            {
                if (workItemId == 0)
                {
                    response.Details.Add("Work item ID is required to check if release plan is ready for SDK generation.");
                    return response;
                }
                
                var releasePlan = await devopsService.GetReleasePlanForWorkItemAsync(workItemId);

                var sdkInfoList = releasePlan?.SDKInfo;

                if (sdkInfoList == null || sdkInfoList.Count == 0)
                {
                    response.Details.Add($"SDK details are not present in the release plan.");
                    return response;
                }

                var sdkInfo = sdkInfoList.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));

                if (sdkInfo == null || string.IsNullOrWhiteSpace(sdkInfo.Language))
                {
                    response.Details.Add($"Release plan work item with ID {workItemId} does not have a language specified. Update SDK details in the release plan.");
                    return response;
                }

                if (string.IsNullOrWhiteSpace(sdkInfo.PackageName))
                {
                    response.Details.Add($"Release plan work item with ID {workItemId} does not have a package name specified for {sdkInfo.Language}. Update SDK details in the release plan.");
                    return response;
                }

                response.Details.Add($"SDK info for language '{sdkInfo.Language}' and package '{sdkInfo.PackageName}' is set correctly in the release plan.");
                response.Status = "Success";
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Details.Add($"Failed to check if Release Plan is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }
        // disabling analyzer warning for MCP001 because the called function is in an entire try/catch block.
#pragma warning disable MCP001
        [McpServerTool, Description("Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params.")]
        public async Task<string> CheckApiReadyForSDKGeneration(string typeSpecProjectRoot, int pullRequestNumber, int workItemId = 0)
#pragma warning restore MCP001
        {
            var response = await IsSpecReadyToGenerateSDKAsync(typeSpecProjectRoot, pullRequestNumber);
            if (workItemId != 0 && response.Status == "Success")
            {
                await devopsService.UpdateApiSpecStatusAsync(workItemId, "Approved");
            }
            return output.Format(response);
        }

        private async Task<GenericResponse> IsSpecReadyToGenerateSDKAsync(string typeSpecProjectRoot, int pullRequestNumber)
        {
            var response = new GenericResponse()
            {
                Status = "Failed"
            };

            try
            {
                if (string.IsNullOrEmpty(typeSpecProjectRoot) && pullRequestNumber == 0)
                {
                    response.Details.Add("Invalid value for both TypeSpec project root and pull request number. Provide at least the TypeSpec project root path for modified project or provide a pull request number.");
                    return response;
                }

                // Get current branch name
                var repoRootPath = typespecHelper.GetSpecRepoRootPath(typeSpecProjectRoot);
                var branchName = gitHelper.GetBranchName(repoRootPath);

                // Check if current repo is private or public repo
                if (!typespecHelper.IsRepoPathForPublicSpecRepo(repoRootPath))
                {
                    response.Details.AddRange([
                        $"Current repo root path '{repoRootPath}' is not a GitHub clone of 'Azure/azure-rest-api-specs' repo. SDK can be generated only if your TypeSpec changes are in public Azure/azure-rest-api-specs repo. ",
                        "Create a pull request in public repo Azure/azure-rest-api-specs for your TypeSpec changes to get your TypeSpec ready."
                        ]);
                    return response;
                }

                if (!typespecHelper.IsValidTypeSpecProjectPath(typeSpecProjectRoot))
                {
                    response.Details.Add($"TypeSpec project path '{typeSpecProjectRoot}' is invalid. Provide a TypeSpec project path that contains tspconfig.yaml");
                    return response;
                }


                // if current branch name is main then ask user to provide pull request number if they have or switch to the branch they have created for TypeSpec changes.
                if (branchName.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"The current branch is '{DEFAULT_BRANCH}', which is not recommended for development. Please switch to a branch containing your TypeSpec project changes or create a new branch if none exists.");
                    return response;
                }

                // Get pull request details
                Octokit.PullRequest? pullRequest = pullRequestNumber != 0 ? await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber) :
                    await githubService.GetPullRequestForBranchAsync(REPO_OWNER, PUBLIC_SPECS_REPO, branchName);
                if (pullRequest == null)
                {
                    response.Details.Add($"Pull request is not found in {REPO_OWNER}/{PUBLIC_SPECS_REPO} for your TypeSpec changes.");
                    if (pullRequestNumber == 0)
                        response.Details.Add("Do you have a pull request created for your TypeSpec changes? If not, make TypeSpec changes for your API specification and create a pull request.");
                    else
                        response.Details.Add($"Pull request {pullRequestNumber} is not valid. Please provide a valid pull request number to check the status.");
                    return response;
                }

                // Pull request is not targeted to main branch
                if (!string.IsNullOrEmpty(pullRequest.Base?.Ref) && !pullRequest.Base.Ref.Equals(DEFAULT_BRANCH))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} merges changes to '{pullRequest.Base?.Ref}' branch. SDK can be generated only from a pull request with {DEFAULT_BRANCH} branch as target. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                // PR closed without merging changes
                if (pullRequest.State == Octokit.ItemState.Closed && !pullRequest.Merged)
                {
                    response.Details.Add($"Pull request {pullRequest.Number} is in closed status without merging changes to main branch. SDK can not be generated from closed PR. Create a pull request for your changes with '{DEFAULT_BRANCH}' branch as target.");
                    return response;
                }

                var isMgmtPlane = typespecHelper.IsTypeSpecProjectForMgmtPlane(typeSpecProjectRoot);
                // Check if ARM or API stewardship approval is present if PR is not in merged status
                // Check ARM approval label is present on the management pull request
                if (!pullRequest.Merged && isMgmtPlane && (pullRequest.Labels == null || !pullRequest.Labels.Any(l => l.Name.Equals(ARM_SIGN_OFF_LABEL))))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have ARM approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    return response;
                }

                // Check if API stewardship approval label is present on the data plane pull request
                if (!pullRequest.Merged && !isMgmtPlane && (pullRequest.Labels == null || !pullRequest.Labels.Any(l => l.Name.Equals(API_STEWARDSHIP_APPROVAL))))
                {
                    response.Details.Add($"Pull request {pullRequest.Number} does not have API stewardship approval. Your API spec changes are not ready to generate SDK. Please check pull request details to get more information on next step for your pull request");
                    return response;
                }

                var approvalLabel = isMgmtPlane ? ARM_SIGN_OFF_LABEL : API_STEWARDSHIP_APPROVAL;
                response.Details.Add($"Pull request {pullRequest.Number} has {approvalLabel} or it is in merged status. Your API spec changes are ready to generate SDK. Please make sure you have a release plan created for the pull request.");
                response.Status = "Success";
                return response;
            }
            catch (Exception ex)
            {
                response.Status = "Failed";
                response.Details.Add($"Failed to check if TypeSpec is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }


        [McpServerTool(Name ="RunGenerateSdk"), Description("Generate SDK from a TypeSpec project using pipeline.")]
        public async Task<string> RunGenerateSdkAsync(string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int pullRequestNumber = 0, int workItemId = 0)
        {
            try
            {
                var response = new GenericResponse()
                {
                    Status = "Success"
                };
                logger.LogInformation($"Generating SDK for TypeSpec project: {typespecProjectRoot}, API Version: {apiVersion}, SDK Release Type: {sdkReleaseType}, Language: {language}, Pull Request Number: {pullRequestNumber}, Work Item ID: {workItemId}");
                // Is language supported for SDK generation
                if (!DevOpsService.IsSDKGenerationSupported(language))
                {
                    response.Details.Add($"SDK generation is currently not supported by agent for {language}");
                    response.Status = "Failed";
                }

                // Is valid typespec project path
                if (!TypeSpecProject.IsValidTypeSpecProjectPath(typespecProjectRoot))
                {
                    response.Details.Add($"Invalid TypeSpec project root path [{typespecProjectRoot}].");
                    response.Status = "Failed";
                }

                if (string.IsNullOrEmpty(apiVersion))
                {
                    response.Details.Add("API version is required to generate SDK.");
                    response.Status = "Failed";
                }

                List<string> validReleaseTypes = ["beta", "stable"];
                sdkReleaseType = sdkReleaseType?.ToLower() ?? "";
                if (string.IsNullOrEmpty(sdkReleaseType) || !validReleaseTypes.Contains(sdkReleaseType))
                {
                    response.Details.Add("SDK release type must be set as either beta or stable to generate SDK.");
                    response.Status = "Failed";
                }

                // Update SDK details in release plan if work item ID is provided
                if (workItemId > 0)
                {
                    var readiness = await IsSdkDetailsPresentInReleasePlanAsync(workItemId, language);
                    if (!readiness.Status.Equals("Success"))
                    {
                        response.Details.AddRange(readiness.Details);
                        response.Status = "Failed";
                    }
                }

                // Return failure details in case of any failure
                if (response.Status.Equals("Failed"))
                {
                    logger.LogInformation($"SDK generation failed with details: [{string.Join(",", response.Details)}]");
                    return output.Format(response);
                }

                string typeSpecProjectPath = typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot);
                string branchRef = "main";
                if (pullRequestNumber > 0)
                {
                    var pullRequest = await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber);
                    branchRef = (pullRequest?.Merged ?? false) ? pullRequest.Base.Ref : $"refs/pull/{pullRequestNumber}/merge";
                }
                logger.LogInformation("Running SDK generation pipeline");
                var pipelineRun = await devopsService.RunSDKGenerationPipelineAsync(branchRef, typeSpecProjectPath, apiVersion, sdkReleaseType, language, workItemId);
                response = new GenericResponse()
                {
                    Status = "Success",
                    Details = [$"Azure DevOps pipeline {DevOpsService.GetPipelineUrl(pipelineRun.Id)} has been initiated to generate the SDK. Build ID is {pipelineRun.Id}. Once the pipeline job completes, an SDK pull request for {language} will be created."]
                };
                return output.Format(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse();
                errorResponse.Details.Add($"Failed to run pipeline to generate SDK, Details: {ex.Message}");
                errorResponse.Status = "Failed";
                return output.Format(errorResponse);
            }
        }

        /// <summary>
        /// Get SDK generation pipeline run details and status for a given pipeline build ID
        /// </summary>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <returns></returns>
        [McpServerTool, Description("Get SDK generation pipeline or release pipeline details and status for a given pipeline build ID")]
        public async Task<string> GetPipelineRunStatus(int buildId)
        {
                
            try
            {
                var response = new GenericResponse();
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
                if (pipeline != null)
                {
                    response.Status = pipeline.Result?.ToString() ?? pipeline.Status?.ToString() ?? "Not available";
                    response.Details.Add($"Pipeline run link: {DevOpsService.GetPipelineUrl(pipeline.Id)}");
                }
                return output.Format(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new GenericResponse();
                errorResponse.Status = "Failed";
                errorResponse.Details.Add($"Failed to get pipeline run with id {buildId}. Error: {ex.Message}");
                return output.Format(errorResponse);
            }
        }

        /// <summary>
        /// Get SDK pull request link from SDK generation pipeline.
        /// </summary>
        /// <param name="language">SDK Language</param>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <param name="workItemId">Work item ID for the release plan</param>
        /// <returns></returns>
        [McpServerTool, Description("Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item.")]
        public async Task<string> GetSDKPullRequestDetails(string language, int workItemId, int buildId = 0)
        {
            try
            {
                if (!IsValidLanguage(language))
                {
                    return $"Unsupported language to get pull request details. Supported languages: {string.Join(", ", SUPPORTED_LANGUAGES)}";
                }

                if (buildId == 0 && workItemId == 0)
                {
                    return "Either build ID or release plan work item ID is required to get SDK pull request details.";
                }

                StringBuilder sb = new ();

                // Get SDK details from work item
                if (buildId == 0)
                {
                    sb.AppendLine("Build Id is not available. Checking for SDK pull request details in release plan work item.");
                    var releasePlan = await devopsService.GetReleasePlanForWorkItemAsync(workItemId);
                    var sdkInfo = releasePlan?.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));
                    if (sdkInfo != null && !string.IsNullOrEmpty(sdkInfo.SdkPullRequestUrl))
                    {
                        sb.AppendLine($"SDK pull request details for {language}: {sdkInfo.SdkPullRequestUrl}");
                    }
                    else
                    {
                        sb.AppendLine($"No SDK pull request details found for {language} in release plan work item.");
                    }

                    return sb.ToString();
                }

                // Find SDK details from build pipeline run
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
                if (pipeline == null)
                {
                    return $"Failed to get SDK generation pipeline run with build ID {buildId}";
                }

                if (pipeline.Status != BuildStatus.Completed)
                {
                    return $"SDK generation pipeline is not in completed status to get generated SDK pull request details, Status: {pipeline.Status.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                }

                if (pipeline.Result != BuildResult.Succeeded && pipeline.Result != BuildResult.PartiallySucceeded)
                {
                    return $"SDK generation pipeline did not succeed. Status: {pipeline.Result?.ToString()}. For more details: {DevOpsService.GetPipelineUrl(buildId)}";
                }

                var data = await devopsService.GetSDKPullRequestFromPipelineRunAsync(buildId, language, workItemId);
                return data;
            }
            catch (Exception ex)
            {
                SetFailure();
                return $"Failed to get pull request details from SDK generation pipeline, Error: {ex.Message}";
            }
        }

        public static bool IsValidLanguage(string language)
        {
            return SUPPORTED_LANGUAGES.Contains(language.ToLower());
        }

        private static string GetRepoName(string language)
        {
            return language.ToLower() switch
            {
                ".net" => "azure-sdk-for-net",
                "javascript" => "azure-sdk-for-js",
                _ => $"azure-sdk-for-{language.ToLower()}"
            };
        }

        [McpServerTool, Description("Link SDK pull request to release plan work item")]
        public async Task<string> LinkSdkPullRequestToReleasePlan(string language, string pullRequestUrl, int workItemId = 0, int releasePlanId = 0)
        {
            try
            {
                // work item Id or release plan Id is required to link SDK pull request to release plan
                if (workItemId == 0 && releasePlanId == 0)
                {
                    return "Either work item ID or release plan ID is required to link SDK pull request to release plan.";
                }

                // Verify language and get repo name
                if (!IsValidLanguage(language))
                {
                    return $"Unsupported language to link pull request. Supported languages: {SUPPORTED_LANGUAGES}";
                }
                // Verify SDK pull request URL
                if (string.IsNullOrEmpty(pullRequestUrl))
                {
                    return "SDK pull request URL is required to link it to release plan.";
                }

                // Parse just the pull request link from input
                var repoName = GetRepoName(language);
                var parsedLink = DevOpsService.ParseSDKPullRequestUrl(pullRequestUrl);
                if (!parsedLink.Contains(repoName))
                {
                    return $"Invalid pull request link. Provide a pull request link in SDK repo {repoName}";
                }
                
                // Add PR to release plan
                var releasePlan = workItemId == 0 ? await devopsService.GetReleasePlanAsync(releasePlanId) : await devopsService.GetReleasePlanForWorkItemAsync(workItemId);
                if (releasePlan == null || releasePlan.WorkItemId == 0)
                {
                    return $"Release plan with ID {releasePlanId} or work item ID {workItemId} is not found.";
                }

                await devopsService.AddSdkInfoInReleasePlanAsync(releasePlan.WorkItemId, language, "", parsedLink);
                return $"Successfully linked pull request to release plan {releasePlan.ReleasePlanId}, work item id {releasePlan.WorkItemId}";
            }
            catch(Exception ex)
            {
                SetFailure();
                return $"Failed to link SDK pull request to release plan work item, Error: {ex.Message}";
            }
        }

        public override Command GetCommand()
        {
            var command = new Command("spec-workflow", "Tools to help with the TypeSpec SDK generation.");
            var subCommands = new[]
            {
                new Command(checkApiReadinessCommandName, "Check if API spec is ready to generate SDK") { typeSpecProjectPathOpt, pullRequestNumberOpt, workItemIdOpt },
                new Command(generateSdkCommandName, "Generate SDK for a TypeSpec project") { typeSpecProjectPathOpt, apiVersionOpt, sdkReleaseTypeOpt, languageOpt, pullRequestNumberOpt, workItemIdOpt },
                new Command(getPipelineStatusCommandName, "Get SDK generation pipeline run status") { pipelineRunIdOpt },
                new Command(getSdkPullRequestCommandName, "Get SDK pull request link from SDK generation pipeline") { languageOpt, pipelineRunIdOpt, workItemIdOpt },
                new Command(linkSdkPrCommandName, "Link SDK pull request to release plan.") {languageOpt, urlOpt, workItemOptionalIdOpt, releasePlanIdOpt }
            };

            foreach (var subCommand in subCommands)
            {
                subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
                command.AddCommand(subCommand);
            }
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            var command = ctx.ParseResult.CommandResult.Command.Name;
            var commandParser = ctx.ParseResult;
            switch (command)
            {
                case checkApiReadinessCommandName:
                    var isSpecReady = await CheckApiReadyForSDKGeneration(commandParser.GetValueForOption(typeSpecProjectPathOpt), pullRequestNumber: commandParser.GetValueForOption(pullRequestNumberOpt), workItemId: commandParser.GetValueForOption(workItemIdOpt));
                    output.Output($"Is API spec ready for SDK generation: {isSpecReady}");
                    return;
                case generateSdkCommandName:
                    var sdkGenerationResponse = await RunGenerateSdkAsync(commandParser.GetValueForOption(typeSpecProjectPathOpt),
                        commandParser.GetValueForOption(apiVersionOpt),
                        commandParser.GetValueForOption(sdkReleaseTypeOpt),
                        commandParser.GetValueForOption(languageOpt),
                        commandParser.GetValueForOption(pullRequestNumberOpt),
                        commandParser.GetValueForOption(workItemIdOpt));
                    output.Output($"SDK generation response: {sdkGenerationResponse}");
                    return;
                case getPipelineStatusCommandName:
                    var pipelineRunStatus = await GetPipelineRunStatus(commandParser.GetValueForOption(pipelineRunIdOpt));
                    output.Output($"SDK generation pipeline run status: {pipelineRunStatus}");
                    return;
                case getSdkPullRequestCommandName:
                    var sdkPullRequestDetails = await GetSDKPullRequestDetails(commandParser.GetValueForOption(languageOpt), workItemId: commandParser.GetValueForOption(workItemIdOpt), buildId: commandParser.GetValueForOption(pipelineRunIdOpt));
                    output.Output($"SDK pull request details: {sdkPullRequestDetails}");
                    return;
                case linkSdkPrCommandName:
                    var linkStatus = await LinkSdkPullRequestToReleasePlan(commandParser.GetValueForOption(languageOpt), commandParser.GetValueForOption(urlOpt), workItemId: commandParser.GetValueForOption(workItemOptionalIdOpt), releasePlanId: commandParser.GetValueForOption(releasePlanIdOpt));
                    output.Output($"Link status: {linkStatus}");
                    return;
                default:
                    SetFailure();
                    output.OutputError($"Unknown command: '{command}'");
                    return;
            }
        }
    }
}
