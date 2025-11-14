// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text;
using Microsoft.TeamFoundation.Build.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Tools.ReleasePlan
{
    [Description("This type contains the MCP tool to run SDK generation using pipeline, check SDK generation pipeline status and to get generated SDK pull request details.")]
    [McpServerToolType]
    public class SpecWorkflowTool(IGitHubService githubService,
        IDevOpsService devopsService,
        IGitHelper gitHelper,
        ITypeSpecHelper typespecHelper,
        ILogger<SpecWorkflowTool> logger,
        IInputSanitizer inputSanitizer
    ) : MCPMultiCommandTool
    {
        public override CommandGroup[] CommandHierarchy { get; set; } = [new("spec-workflow", "TypeSpec SDK generation commands")];

        // Commands
        private const string checkApiReadinessCommandName = "check-api-readiness";
        private const string generateSdkCommandName = "generate-sdk";
        private const string getSdkPullRequestCommandName = "get-sdk-pr";
        private const string linkSdkPrCommandName = "link-sdk-pr";

        // Options
        private readonly Option<string> typeSpecProjectPathOpt = new("--typespec-project")
        {
            Description = "Path to typespec project",
            Required = true,
        };

        private readonly Option<int> pullRequestNumberOpt = new("--pr")
        {
            Description = "Pull request number",
            Required = false,
        };

        private readonly Option<string> apiVersionOpt = new("--api-version")
        {
            Description = "API version",
            Required = true,
        };

        private readonly Option<string> sdkReleaseTypeOpt = new("--release-type")
        {
            Description = "SDK release type: beta or stable",
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
            Required = false,
        };

        private readonly Option<int> pipelineRunIdOpt = new("--pipeline-run")
        {
            Description = "SDK generation pipeline run id",
            Required = true,
        };

        private readonly Option<string> urlOpt = new("--url")
        {
            Description = "Pull request url",
            Required = true,
        };

        private readonly Option<int> releasePlanIdOpt = new("--release-plan")
        {
            Description = "SDK release plan id",
            Required = false,
        };

        private readonly Option<int> workItemOptionalIdOpt = new("--workitem-id")
        {
            Description = "Release plan work item id",
            Required = false,
        };

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

        protected override List<Command> GetCommands() =>
        [
            new(checkApiReadinessCommandName, "Check if API spec is ready to generate SDK")
            {
                typeSpecProjectPathOpt, pullRequestNumberOpt, workItemIdOpt,
            },
            new(generateSdkCommandName, "Generate SDK for a TypeSpec project")
            {
                typeSpecProjectPathOpt, apiVersionOpt, sdkReleaseTypeOpt, languageOpt, pullRequestNumberOpt, workItemIdOpt,
            },
            new(getSdkPullRequestCommandName, "Get SDK pull request link from SDK generation pipeline")
            {
                languageOpt, pipelineRunIdOpt, workItemIdOpt,
            },
            new(linkSdkPrCommandName, "Link SDK pull request to release plan")
            {
                languageOpt, urlOpt, workItemOptionalIdOpt, releasePlanIdOpt,
            }
        ];

        public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
        {
            var command = parseResult.CommandResult.Command.Name;
            var commandParser = parseResult;
            return command switch
            {
                checkApiReadinessCommandName => await CheckApiReadyForSDKGeneration(commandParser.GetValue(typeSpecProjectPathOpt), pullRequestNumber: commandParser.GetValue(pullRequestNumberOpt), workItemId: commandParser.GetValue(workItemIdOpt)),
                generateSdkCommandName => await RunGenerateSdkAsync(commandParser.GetValue(typeSpecProjectPathOpt),
                                        commandParser.GetValue(apiVersionOpt),
                                        commandParser.GetValue(sdkReleaseTypeOpt),
                                        commandParser.GetValue(languageOpt),
                                        commandParser.GetValue(pullRequestNumberOpt),
                                        commandParser.GetValue(workItemIdOpt)),
                getSdkPullRequestCommandName => await GetSDKPullRequestDetails(commandParser.GetValue(languageOpt), workItemId: commandParser.GetValue(workItemIdOpt), buildId: commandParser.GetValue(pipelineRunIdOpt)),
                linkSdkPrCommandName => await LinkSdkPullRequestToReleasePlan(commandParser.GetValue(languageOpt), commandParser.GetValue(urlOpt), workItemId: commandParser.GetValue(workItemOptionalIdOpt), releasePlanId: commandParser.GetValue(releasePlanIdOpt)),
                _ => new DefaultCommandResponse { ResponseError = $"Unknown command: '{command}'" },
            };
        }

        private async Task<ReleaseWorkflowResponse> IsSdkDetailsPresentInReleasePlanAsync(int workItemId, string language)
        {
            var response = new ReleaseWorkflowResponse()
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
                    response.Details.Add($"SDK details are not present in the release plan. Update the SDK details using the information in tspconfig.yaml");
                    return response;
                }

                var sdkInfo = sdkInfoList.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));

                if (sdkInfo == null || string.IsNullOrWhiteSpace(sdkInfo.Language))
                {
                    response.Details.Add($"Release plan work item with ID {workItemId} does not have a language specified. Update the SDK details using the information in tspconfig.yaml.");
                    return response;
                }

                if (string.IsNullOrWhiteSpace(sdkInfo.PackageName))
                {
                    response.Details.Add($"Release plan work item with ID {workItemId} does not have a package name specified for {sdkInfo.Language}. Update the SDK details using the information in tspconfig.yaml.");
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
                response.Details.Add($"Failed to check if Release Plan is ready for SDK generation. Error: {ex.Message}");
                return response;
            }
        }

        [McpServerTool(Name = "azsdk_check_api_spec_ready_for_sdk"), Description("Checks whether a TypeSpec API spec is ready to generate SDK. Provide a pull request number and path to TypeSpec project json as params.")]
        public async Task<ReleaseWorkflowResponse> CheckApiReadyForSDKGeneration(string typeSpecProjectRoot, int pullRequestNumber, int workItemId = 0)
        {
            try
            {
                var response = await IsSpecReadyToGenerateSDKAsync(typeSpecProjectRoot, pullRequestNumber);
                if (workItemId != 0 && response.Status == "Success")
                {
                    await devopsService.UpdateApiSpecStatusAsync(workItemId, "Approved");
                }
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check if API spec is ready for SDK generation");
                return new ReleaseWorkflowResponse
                {
                    ResponseError = $"Failed to check if API spec is ready for SDK generation: {ex.Message}",
                };
            }
        }

        private async Task<ReleaseWorkflowResponse> IsSpecReadyToGenerateSDKAsync(string typeSpecProjectRoot, int pullRequestNumber)
        {
            var response = new ReleaseWorkflowResponse()
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
                response.TypeSpecProject = typespecHelper.GetTypeSpecProjectRelativePath(typeSpecProjectRoot);

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
                    {
                        response.Details.Add("Do you have a pull request created for your TypeSpec changes? If not, make TypeSpec changes for your API specification and create a pull request.");
                    }
                    else
                    {
                        response.Details.Add($"Pull request {pullRequestNumber} is not valid. Please provide a valid pull request number to check the status.");
                    }
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
                response.PackageType = isMgmtPlane ? SdkType.Management : SdkType.Dataplane;
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


        [McpServerTool(Name = "azsdk_run_generate_sdk"), Description("Generate SDK from a TypeSpec project using pipeline.")]
        public async Task<ReleaseWorkflowResponse> RunGenerateSdkAsync(string typespecProjectRoot, string apiVersion, string sdkReleaseType, string language, int pullRequestNumber = 0, int workItemId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse()
                {
                    Status = "Success"
                };
                language = inputSanitizer.SanitizeName(language);
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
                    response.Details.Add($"SDK generation is currently not supported by agent for {language}");
                    response.Status = "Failed";
                }
                response.SetLanguage(language);
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
                    response.PackageType = readiness.PackageType;
                }

                if (workItemId > 0 && pullRequestNumber > 0)
                {
                    var apiReadiness = await CheckApiReadyForSDKGeneration(typespecProjectRoot, pullRequestNumber, workItemId);
                    response.Details.AddRange(apiReadiness.ToString().Split(Environment.NewLine));
                }
                // Return failure details in case of any failure
                if (response.Status.Equals("Failed"))
                {
                    var failureDetails = string.Join(",", response.Details);
                    logger.LogInformation("SDK generation failed with details: [{FailureDetails}]", failureDetails);
                    return response;
                }

                string typeSpecProjectPath = typespecHelper.GetTypeSpecProjectRelativePath(typespecProjectRoot);
                string apiSpecBranchRef = "main";
                if (pullRequestNumber > 0)
                {
                    var pullRequest = await githubService.GetPullRequestAsync(REPO_OWNER, PUBLIC_SPECS_REPO, pullRequestNumber);
                    apiSpecBranchRef = (pullRequest?.Merged ?? false) ? pullRequest.Base.Ref : $"refs/pull/{pullRequestNumber}/merge";
                }

                string sdkRepoBranch = "";
                var releasePlan = workItemId != 0 ? await devopsService.GetReleasePlanForWorkItemAsync(workItemId) : null;
                var sdkPullRequestUrl = releasePlan?.SDKInfo.FirstOrDefault(s => s.Language == language)?.SdkPullRequestUrl;
                if (!string.IsNullOrEmpty(sdkPullRequestUrl))
                {
                    var parsedUrl = DevOpsService.ParseSDKPullRequestUrl(sdkPullRequestUrl);
                    var sdkPullRequest = await githubService.GetPullRequestAsync(parsedUrl.RepoOwner, parsedUrl.RepoName, parsedUrl.PrNumber);
                    if (sdkPullRequest is not null && sdkPullRequest.State != "closed" && sdkPullRequest.Merged == false)
                    {
                        sdkRepoBranch = sdkPullRequest.Head.Ref;
                    }
                }
                response.TypeSpecProject = typeSpecProjectPath;
                logger.LogInformation("Running SDK generation pipeline");
                var pipelineRun = await devopsService.RunSDKGenerationPipelineAsync(apiSpecBranchRef, typeSpecProjectPath, apiVersion, sdkReleaseType, language, workItemId, sdkRepoBranch);
                response = new ReleaseWorkflowResponse()
                {
                    Status = "Success",
                    Details = [$"Azure DevOps pipeline {DevOpsService.GetPipelineUrl(pipelineRun.Id)} has been initiated to generate the SDK. Build ID is {pipelineRun.Id}. Once the pipeline job completes, an SDK pull request for {language} will be created."]
                };
                return response;
            }
            catch (Exception ex)
            {
                var errorResponse = new ReleaseWorkflowResponse();
                errorResponse.Details.Add($"Failed to run pipeline to generate SDK, Details: {ex.Message}");
                errorResponse.Status = "Failed";
                errorResponse.ExitCode = 1;
                return errorResponse;
            }
        }


        /// <summary>
        /// Get SDK pull request link from SDK generation pipeline.
        /// </summary>
        /// <param name="language">SDK Language</param>
        /// <param name="buildId">Build ID for the pipeline run</param>
        /// <param name="workItemId">Work item ID for the release plan</param>
        /// <returns></returns>
        [McpServerTool(Name = "azsdk_get_sdk_pull_request_link"), Description("Get SDK pull request link from SDK generation pipeline run or from work item. Build ID of pipeline run is required to query pull request link from SDK generation pipeline. This tool can get SDK pull request details if present in a work item.")]
        public async Task<ReleaseWorkflowResponse> GetSDKPullRequestDetails(string language, int workItemId, int buildId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse();
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

                response.SetLanguage(language);
                // Get SDK details from work item
                if (buildId == 0)
                {
                    response.Details.Add("Build Id is not available. Checking for SDK pull request details in release plan work item.");
                    var releasePlan = await devopsService.GetReleasePlanForWorkItemAsync(workItemId);
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
                var pipeline = await devopsService.GetPipelineRunAsync(buildId);
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

                var pr = await devopsService.GetSDKPullRequestFromPipelineRunAsync(buildId, language, workItemId);
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

        private static string GetRepoName(string language)
        {
            return language.ToLower() switch
            {
                ".net" => "azure-sdk-for-net",
                "javascript" => "azure-sdk-for-js",
                _ => $"azure-sdk-for-{language.ToLower()}"
            };
        }

        [McpServerTool(Name = "azsdk_link_sdk_pull_request_to_release_plan"), Description("Link SDK pull request to release plan work item")]
        public async Task<ReleaseWorkflowResponse> LinkSdkPullRequestToReleasePlan(string language, string pullRequestUrl, int workItemId = 0, int releasePlanId = 0)
        {
            try
            {
                var response = new ReleaseWorkflowResponse();
                // work item Id or release plan Id is required to link SDK pull request to release plan
                if (workItemId == 0 && releasePlanId == 0)
                {
                    response.ResponseError = "Either work item ID or release plan ID is required to link SDK pull request to release plan.";
                    return response;
                }

                // Verify language and get repo name
                if (!IsValidLanguage(language))
                {
                    response.ResponseError = $"Unsupported language to link pull request. Supported languages: {string.Join(", ", SUPPORTED_LANGUAGES)}";
                    return response;
                }
                // Verify SDK pull request URL
                if (string.IsNullOrEmpty(pullRequestUrl))
                {
                    response.ResponseError = "SDK pull request URL is required to link it to release plan.";
                    return response;
                }

                // Parse just the pull request link from input
                var repoName = GetRepoName(language);
                var parsedLink = DevOpsService.ParseSDKPullRequestUrl(pullRequestUrl);
                if (!parsedLink.FullUrl.Contains(repoName))
                {
                    response.ResponseError = $"Invalid pull request link. Provide a pull request link in SDK repo {repoName}";
                    return response;
                }

                // Add PR to release plan
                var releasePlan = workItemId == 0 ? await devopsService.GetReleasePlanAsync(releasePlanId) : await devopsService.GetReleasePlanForWorkItemAsync(workItemId);
                if (releasePlan == null || releasePlan.WorkItemId == 0)
                {
                    response.ResponseError = $"Release plan with ID {releasePlanId} or work item ID {workItemId} is not found.";
                    return response;
                }

                var sdkInfoInRelease = devopsService.AddSdkInfoInReleasePlanAsync(releasePlan.WorkItemId, language, "", parsedLink.FullUrl);
                var releaseInfoInSdk = UpdateSdkPullRequestDescription(parsedLink, releasePlan);

                await Task.WhenAll(sdkInfoInRelease, releaseInfoInSdk);
                response.SetLanguage(language);
                if (releasePlan.IsManagementPlane)
                {
                    response.PackageType = SdkType.Management;
                }
                else if (releasePlan.IsDataPlane)
                {
                    response.PackageType = SdkType.Dataplane;
                }
                response.Details.Add($"Successfully linked pull request to release plan {releasePlan.ReleasePlanId}, work item id {releasePlan.WorkItemId}, and updated PR description.");
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to link SDK pull request to release plan work item");
                return new() { ResponseError = $"Failed to link SDK pull request to release plan work item, Error: {ex.Message}" };
            }
        }

        private async Task UpdateSdkPullRequestDescription(ParsedSdkPullRequest parsedUrl, ReleasePlanDetails releasePlan)
        {
            var repoOwner = parsedUrl.RepoOwner;
            var repoName = parsedUrl.RepoName;
            var prNumber = parsedUrl.PrNumber;

            var pr = await githubService.GetPullRequestAsync(repoOwner, repoName, prNumber);
            if (pr == null)
            {
                throw new InvalidOperationException($"Failed to fetch pull request {repoOwner}/{repoName}#{prNumber}");
            }

            // Check if the PR body already contains the release plan link (main indicator)
            var header = "## Release Plan Details";
            if (!string.IsNullOrEmpty(pr.Body) && pr.Body.Contains(header, StringComparison.OrdinalIgnoreCase))
            {
                // If already contains release plan info, just return without doing anything
                return;
            }

            var linksBuilder = new StringBuilder(header);
            linksBuilder.AppendLine();
            linksBuilder.AppendLine($"- Release Plan: {releasePlan.ReleasePlanLink}");
            linksBuilder.AppendLine($"- Work Item Link: {releasePlan.WorkItemHtmlUrl}");
            linksBuilder.AppendLine($"- Spec Pull Request: {releasePlan.ActiveSpecPullRequest}");
            linksBuilder.Append($"- Spec API version: {releasePlan.SpecAPIVersion}");

            var links = linksBuilder.ToString();
            var appendedBody = string.IsNullOrEmpty(pr.Body)
                ? links
                : $"{pr.Body}\n{links}";
            try
            {
                await githubService.UpdatePullRequestAsync(repoOwner, repoName, prNumber, pr.Title, appendedBody, pr.State.Value);
            }
            catch (Exception ex)
            {
                // This should not be a hard error when context is not updated in PR description
                logger.LogError(ex, "Failed to update pull request description for {repoOwner}/{repoName}#{prNumber}", repoOwner, repoName, prNumber);
                return;
            }
        }
    }
}
